using ERP.PRM.Infrastructure.Persistence;
using ERP.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace ERP.PRM.Infrastructure.BackgroundJobs
{
    public class OutboxProcessor : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OutboxProcessor> _logger;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _executingTask;

        public OutboxProcessor(IServiceProvider serviceProvider, ILogger<OutboxProcessor> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _executingTask = ExecuteAsync(_cancellationTokenSource.Token);

            return _executingTask.IsCompleted ? _executingTask : Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_executingTask == null) return;

            _cancellationTokenSource?.Cancel();
            await Task.WhenAny(_executingTask, Task.Delay(Timeout.Infinite, cancellationToken));

            _cancellationTokenSource?.Dispose();
        }

        private async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<PromotionDbContext>();
                    var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

                    var unprocessedMessages = await dbContext.OutboxMessages
                        .Where(m => m.ProcessedOnUtc == null)
                        .OrderBy(m => m.OccurredOnUtc)
                        .ToListAsync(cancellationToken);

                    foreach (var message in unprocessedMessages)
                    {
                        try
                        {
                            var eventType = Type.GetType(message.Type);
                            if (eventType == null)
                            {
                                _logger.LogWarning("Event type {EventType} not found", message.Type);
                                message.ProcessedOnUtc = DateTime.UtcNow;
                                message.Error = $"Event type not found: {message.Type}";
                                continue;
                            }

                            var @event = JsonConvert.DeserializeObject(message.Content, eventType, new JsonSerializerSettings
                            {
                                TypeNameHandling = TypeNameHandling.All
                            });

                            if (@event != null)
                            {
                                await messageBus.PublishAsync(@event, cancellationToken);
                                message.ProcessedOnUtc = DateTime.UtcNow;
                                _logger.LogInformation("Outbox message {MessageId} published successfully", message.Id);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error publishing outbox message {MessageId}", message.Id);
                            message.Error = ex.Message;
                        }
                    }

                    await dbContext.SaveChangesAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in outbox processor");
                }

                await Task.Delay(5000, cancellationToken);
            }
        }
    }
}
