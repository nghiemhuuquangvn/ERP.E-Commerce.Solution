using Confluent.Kafka;
using ERP.CRM.Application.Interfaces;
using ERP.CRM.Infrastructure.MessageBus;
using ERP.CRM.Infrastructure.Persistence;
using ERP.CRM.Infrastructure.Repositories;
using ERP.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

            // Repositories
            services.AddScoped<IContactRepository, ContactRepository>();
            services.AddScoped<ILeadRepository, LeadRepository>();
            services.AddScoped<IOpportunityRepository, OpportunityRepository>();
            services.AddScoped<ICaseRepository, CaseRepository>();

            return services;
        }
    }
}
