using ERP.LOG.Application.Interfaces;
using ERP.LOG.Infrastructure.EventConsumers;
using ERP.LOG.Infrastructure.Search;
using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.LOG.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var elasticsearchUri = configuration.GetValue<string>("Elasticsearch:Uri") ?? "http://localhost:9200";
        services.AddSingleton(new ElasticsearchClient(new Uri(elasticsearchUri)));

        services.AddScoped<ILogQueryRepository, ElasticsearchLogRepository>();
        services.AddHostedService<LogIndexTemplateInitializer>();
        services.AddHostedService<SystemLogsKafkaConsumer>();

        return services;
    }
}
