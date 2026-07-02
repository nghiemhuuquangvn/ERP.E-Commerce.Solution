using Confluent.Kafka;
using ERP.CRM.Application.EventHandlers;
using ERP.CRM.Application.Interfaces;
using ERP.CRM.Infrastructure.EventBus;
using ERP.CRM.Infrastructure.MessageBus;
using ERP.CRM.Infrastructure.Persistence;
using ERP.CRM.Infrastructure.Repositories;
using ERP.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;

namespace ERP.CRM.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("CrmDB");
            services.AddDbContext<CrmDbContext>(options =>
                options.UseSqlServer(connectionString));

            var kafkaServers = configuration["KafkaSettings:BootstrapServers"] ?? "localhost:9092";

            var producerConfig = new ProducerConfig
            {
                BootstrapServers = kafkaServers,
                AllowAutoCreateTopics = true
            };

            services.AddSingleton(new ProducerBuilder<string, string>(producerConfig).Build());
            services.AddScoped<IMessageBus, KafkaMessageBus>();

            services.AddHostedService<BackgroundJobs.OutboxProcessor>();

            // RabbitMQ connection — shared single connection, matching the topic exchange
            // model SaleOrderService's RabbitMqMessageBus / WarehouseService's consumer use.
            services.AddSingleton<IConnection>(_ =>
            {
                var factory = new ConnectionFactory
                {
                    HostName = configuration["RabbitMQ:HostName"] ?? "localhost",
                    Port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672"),
                    UserName = configuration["RabbitMQ:UserName"] ?? "guest",
                    Password = configuration["RabbitMQ:Password"] ?? "guest",
                    VirtualHost = configuration["RabbitMQ:VirtualHost"] ?? "/",
                    DispatchConsumersAsync = true
                };
                return factory.CreateConnection();
            });

            // Inbound consumers: identity-topic + payment-topic (Kafka) and
            // sales_order_events (RabbitMQ) — previously dead handler classes below.
            services.AddHostedService<BackgroundJobs.CrmKafkaConsumer>();
            services.AddHostedService<SalesOrderRabbitMqConsumer>();
            services.AddScoped<UserCreatedEventHandler>();
            services.AddScoped<PaymentProcessedEventHandler>();
            services.AddScoped<SalesOrderCreatedEventHandler>();

            // Repositories
            services.AddScoped<IContactRepository, ContactRepository>();
            services.AddScoped<ILeadRepository, LeadRepository>();
            services.AddScoped<IOpportunityRepository, OpportunityRepository>();
            services.AddScoped<ICaseRepository, CaseRepository>();

            return services;
        }
    }
}
