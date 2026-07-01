using Confluent.Kafka;
using ERP.MST.Application.Interfaces;
using ERP.MST.Infrastructure.BackgroundJobs;
using ERP.MST.Infrastructure.MessageBus;
using ERP.MST.Infrastructure.Persistence;
using ERP.MST.Infrastructure.Repositories;
using ERP.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.MST.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("MasterDataDb");
            services.AddDbContext<MasterDataDbContext>(options =>
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
            services.AddHostedService<HrmEventConsumer>();

            // Repositories
            services.AddScoped<IDepartmentRepository, DepartmentRepository>();
            services.AddScoped<IPositionRepository, PositionRepository>();
            services.AddScoped<IEmployeeRepository, EmployeeRepository>();
            services.AddScoped<IUnitOfMeasureRepository, UnitOfMeasureRepository>();
            services.AddScoped<ICountryRepository, CountryRepository>();
            services.AddScoped<IProvinceRepository, ProvinceRepository>();
            services.AddScoped<IDistrictRepository, DistrictRepository>();
            services.AddScoped<IWardRepository, WardRepository>();
            services.AddScoped<ISupportedLanguageRepository, SupportedLanguageRepository>();

            return services;
        }
    }
}
