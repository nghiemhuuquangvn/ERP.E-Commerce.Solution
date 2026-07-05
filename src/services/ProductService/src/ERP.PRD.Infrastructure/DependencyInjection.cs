using ERP.PRD.Application.Interfaces;
using ERP.PRD.Domain.Search;
using ERP.PRD.Infrastructure.EventBus;
using ERP.PRD.Infrastructure.Persistence;
using ERP.PRD.Infrastructure.Repositories;
using ERP.PRD.Infrastructure.Search;
using ERP.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Confluent.Kafka;
using Elastic.Clients.Elasticsearch;
using RabbitMQ.Client;

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

            // Register Repositories
            services.AddScoped<IProductRepository, ProductRepository>();
            services.AddScoped<ICategoryRepository, CategoryRepository>();
            services.AddScoped<IProductVariantRepository, ProductVariantRepository>();
            services.AddScoped<IProductAttributeRepository, ProductAttributeRepository>();
            services.AddScoped<IProductAttributeValueRepository, ProductAttributeValueRepository>();
            services.AddScoped<IProductImageRepository, ProductImageRepository>();
            services.AddScoped<IPriceListRepository, PriceListRepository>();
            services.AddScoped<IProductBundleRepository, ProductBundleRepository>();
            services.AddScoped<IProductRelationRepository, ProductRelationRepository>();
            services.AddScoped<IProductReviewRepository, ProductReviewRepository>();
            services.AddScoped<ISearchIndexQueueRepository, SearchIndexQueueRepository>();

            // Register Unit of Work
            services.AddScoped<IProductUnitOfWork, ProductUnitOfWork>();

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

            // Register Elasticsearch client for the derived "products" search index
            var elasticsearchUri = configuration.GetValue<string>("Elasticsearch:Uri") ?? "http://localhost:9200";
            services.AddSingleton(new ElasticsearchClient(new Uri(elasticsearchUri)));

            services.AddScoped<IProductSearchIndexer, ElasticsearchProductIndexer>();
            services.AddScoped<IProductSearchService, ElasticsearchProductSearchService>();

            // Register SearchIndexProcessor background job (polls SearchIndexQueue, rebuilds
            // the ES document per pending ProductId — mirrors OutboxProcessor's shape)
            services.AddHostedService<SearchIndexProcessor>();

            // RabbitMQ connection for consuming WarehouseService's warehouse_exchange
            // (stock-quantity sync) — mirrors the shared-connection pattern established
            // in WareHouseService/PaymentService/etc. earlier this session.
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
            services.AddHostedService<WarehouseRabbitMqConsumer>();

            return services;
        }
    }
}
