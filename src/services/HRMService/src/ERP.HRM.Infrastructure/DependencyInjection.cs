using Confluent.Kafka;
using ERP.HRM.Application.Interfaces;
using ERP.HRM.Infrastructure.BackgroundJobs;
using ERP.HRM.Infrastructure.MessageBus;
using ERP.HRM.Infrastructure.Persistence;
using ERP.HRM.Infrastructure.Repositories;
using ERP.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.HRM.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("HrmDB");
            services.AddDbContext<HrmDbContext>(options =>
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
            services.AddHostedService<MasterDataEventConsumer>();

            // Repositories
            services.AddScoped<IEmployeeSnapshotRepository, EmployeeSnapshotRepository>();
            services.AddScoped<ILeaveRequestRepository, LeaveRequestRepository>();
            services.AddScoped<IPaySlipRepository, PaySlipRepository>();
            services.AddScoped<IAttendanceRepository, AttendanceRepository>();
            services.AddScoped<IWorkShiftRepository, WorkShiftRepository>();

            return services;
        }
    }
}
