using Confluent.Kafka;
using ERP.ACC.Infrastructure.MessageBus;
using ERP.ACC.Infrastructure.Persistence;
using ERP.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.ACC.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("AccountingDB");
            services.AddDbContext<AccountingDbContext>(options =>
                options.UseSqlServer(connectionString));

            var kafkaServers = configuration["KafkaSettings:BootstrapServers"] ?? "localhost:9092";

            var producerConfig = new ProducerConfig
            {
                BootstrapServers = kafkaServers,
                AllowAutoCreateTopics = true
            };

            services.AddSingleton(new ProducerBuilder<string, string>(producerConfig).Build());
            services.AddScoped<IMessageBus, KafkaMessageBus>();

            services.AddHostedService<OutboxProcessor>();

            return services;
        }
    }
}
