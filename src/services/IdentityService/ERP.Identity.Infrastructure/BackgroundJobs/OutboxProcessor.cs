using ERP.Identity.Application.Interfaces;
using ERP.Identity.Application.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ERP.Identity.Infrastructure.BackgroundJobs
{
    public class OutboxProcessor : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OutboxProcessor> _logger;

        public OutboxProcessor(ILogger<OutboxProcessor> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Outbox Processor started at: {time}", DateTimeOffset.Now);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // TẠO SCOPE Ở ĐÂY
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        // Lấy Repository và MessageBus từ Scope
                        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
                        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                        // 1. Lấy tin nhắn chưa xử lý
                        var messages = await unitOfWork.OutboxMessages.GetPendingMessagesAsync(stoppingToken);
                        if (!messages.Any())
                        {
                            await Task.Delay(5000, stoppingToken);
                            continue;
                        }

                        foreach (var message in messages)
                        {
                            try
                            {
                                // Gửi lên Kafka
                                await messageBus.PublishAsync("identity-topic", message.Content, stoppingToken);
                                
                                message.ProcessedOnUtc = DateTime.UtcNow;
                                message.Error = null; // Xóa lỗi cũ nếu có
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to publish message {Id}", message.Id);
                                message.Error = ex.Message;
                                // Không throw ở đây để tiếp tục xử lý các message khác trong lô
                            }
                        }

                        // 2. Lưu lại trạng thái của cả lô
                        // Vì chúng ta đang dùng DbContext từ Scope, SaveChanges ở đây 
                        // chỉ cập nhật trạng thái "Đã xử lý" cho bảng Outbox.
                        await unitOfWork.SaveChangesAsync(stoppingToken);
                    }
                    // Khi kết thúc khối using, Scope sẽ bị hủy, Repository và DbContext sẽ được giải phóng sạch sẽ.
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi xử lý Outbox");
                }

                await Task.Delay(5000, stoppingToken);
            }
        }
    }
}