using ERP.PRD.Infrastructure.Persistence;
using ERP.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Confluent.Kafka;

namespace ERP.PRD.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            // Register DbContext
            services.AddDbContext<ProductDbContext>(options =>
            {
                var connectionString = configuration.GetConnectionString("ProductDB");
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "dbo");
                });
            });

            // Register Kafka Message Bus
            var kafkaSettings = configuration.GetSection("KafkaSettings");
            var bootstrapServers = kafkaSettings.GetValue<string>("BootstrapServers") ?? "localhost:9092";

            var producerConfig = new ProducerConfig
            {
                BootstrapServers = bootstrapServers,
                Acks = Acks.All,
                RetryBackoffMs = kafkaSettings.GetValue<int>("RetryBackoffMs", 100),
                MessageTimeoutMs = kafkaSettings.GetValue<int>("MessageTimeoutMs", 30000)
            };

            services.AddSingleton(new ProducerBuilder<string, string>(producerConfig).Build());
            services.AddScoped<IMessageBus, KafkaMessageBus>();

            // Register OutboxProcessor background job
            services.AddHostedService<BackgroundJobs.OutboxProcessor>();

            return services;
        }
    }
}
