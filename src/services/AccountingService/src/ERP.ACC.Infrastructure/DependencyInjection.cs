using Confluent.Kafka;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Infrastructure.Services;
using ERP.ACC.Infrastructure.BackgroundJobs;
using ERP.ACC.Infrastructure.EventBus;
using ERP.ACC.Infrastructure.MessageBus;
using ERP.ACC.Infrastructure.Persistence;
using ERP.ACC.Infrastructure.Repositories;
using ERP.Shared.Infrastructure.Caching;
using ERP.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;

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

            // Inbound consumers: purchasing-topic + payment-topic (Kafka) and
            // sales_order_events (RabbitMQ) — previously dead MediatR handler classes.
            services.AddHostedService<AccountingKafkaConsumer>();
            services.AddHostedService<SalesOrderRabbitMqConsumer>();
            services.AddHostedService<WarehouseRabbitMqConsumer>();

            services.AddScoped<IAccountRepository, AccountRepository>();
            services.AddScoped<IInvoiceRepository, InvoiceRepository>();
            services.AddScoped<IBillRepository, BillRepository>();
            services.AddScoped<IJournalEntryRepository, JournalEntryRepository>();
            services.AddScoped<IJournalLineRepository, JournalLineRepository>();
            services.AddScoped<IFiscalPeriodRepository, FiscalPeriodRepository>();
            services.AddScoped<IPostingAccountMappingRepository, PostingAccountMappingRepository>();
            services.AddScoped<IClosingPolicyRepository, ClosingPolicyRepository>();
            services.AddScoped<IReportProfileRepository, ReportProfileRepository>();
            services.AddScoped<IOffBalanceRepository, OffBalanceRepository>();
            services.AddScoped<IPaymentApplicationRepository, PaymentApplicationRepository>();
            services.AddScoped<IStatementMappingRepository, StatementMappingRepository>();
            services.AddScoped<ILoanRepository, LoanRepository>();
            services.AddScoped<INoteTextRepository, NoteTextRepository>();
            services.AddScoped<ERP.ACC.Application.Posting.IJournalNumberGenerator, ERP.ACC.Infrastructure.Services.JournalNumberGenerator>();
            services.AddScoped<ITaxRateRepository, TaxRateRepository>();
            services.AddScoped<IBankAccountRepository, BankAccountRepository>();
            services.AddScoped<IBankStatementRepository, BankStatementRepository>();
            services.AddScoped<IAccountingUnitOfWork, AccountingUnitOfWork>();

            // Tax Settings (System/Tenant two-tier) — storage+CRUD only for now
            services.AddScoped<ITaxSettingsService, TaxSettingsService>();

            services.AddCacheService(configuration, "acc");

            return services;
        }
    }
}
