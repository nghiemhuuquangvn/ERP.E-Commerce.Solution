using ERP.CHB.Application.Interfaces;
using ERP.CHB.Infrastructure.Gateway;
using ERP.CHB.Infrastructure.LlmProviders;
using ERP.CHB.Infrastructure.Persistence;
using ERP.CHB.Infrastructure.Providers;
using ERP.CHB.Infrastructure.Repositories;
using ERP.CHB.Infrastructure.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace ERP.CHB.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddChatBotInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ChatBotDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("ChatBotDb")
                ?? "Server=(local);Database=ChatBotDB;Trusted_Connection=true;"));

        services.AddScoped<IConversationRepository, ConversationRepository>();

        // Gateway:BaseUrl points at the Ocelot Gateway (e.g. http://gateway:5000 in
        // docker-compose) — every tool call and escalation goes through it like any other
        // client, carrying the caller's own forwarded bearer token (see IErpGatewayClient).
        services.AddHttpClient<IErpGatewayClient, ErpGatewayClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["Gateway:BaseUrl"] ?? "http://gateway:5000");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddScoped<ICrmEscalationClient, CrmEscalationClient>();

        services.AddScoped<IErpTool, GetMyOrdersTool>();
        services.AddScoped<IErpTool, GetOrderByIdTool>();
        services.AddScoped<IErpTool, GetProductInfoTool>();
        services.AddScoped<IErpTool, GetInventoryAvailabilityTool>();
        services.AddScoped<IErpTool, GetShipmentTrackingByOrderTool>();
        services.AddScoped<IErpTool, GetActivePromotionsTool>();
        services.AddScoped<IErpToolRegistry, ErpToolRegistry>();

        services.AddHttpClient<IChatCompletionProvider, AnthropicChatCompletionProvider>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });
        services.AddHttpClient<IChatCompletionProvider, OpenAiChatCompletionProvider>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });
        services.AddScoped<IChatProviderResolver, ChatProviderResolver>();

        return services;
    }
}
