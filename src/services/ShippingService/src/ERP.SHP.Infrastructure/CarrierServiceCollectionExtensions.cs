using ERP.Shared.Security;
using ERP.SHP.Application.Interfaces;
using ERP.SHP.Application.Services;
using ERP.SHP.Infrastructure.BackgroundJobs;
using ERP.SHP.Infrastructure.Carriers;
using ERP.SHP.Infrastructure.Repositories;
using ERP.SHP.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.SHP.Infrastructure;

public static class CarrierServiceCollectionExtensions
{
    /// <summary>
    /// Everything the carrier integration needs, in one place (a DI test resolves it, because a missing registration only shows at the first request):
    /// the four providers, the resolver of the shop's account, booking, tracking, webhooks, rules and the polling job. The carriers' accounts are
    /// encrypted with ISecretProtector, so <c>Security:SettingsEncryptionKey</c> must be configured, as for PaymentService.
    /// </summary>
    public static IServiceCollection AddCarrierIntegration(this IServiceCollection services)
    {
        services.AddHttpClient("Carriers");
        services.AddScoped<ISecretProtector, AesSecretProtector>();

        services.AddScoped<ICarrierProvider, GhtkCarrierProvider>();
        services.AddScoped<ICarrierProvider, GhnCarrierProvider>();
        services.AddScoped<ICarrierProvider, ViettelPostCarrierProvider>();
        services.AddScoped<ICarrierProvider, JAndTCarrierProvider>();
        services.AddScoped<ICarrierProviderResolver, CarrierProviderResolver>();

        services.AddScoped<IShippingConfigRepository, ShippingConfigRepository>();
        services.AddScoped<CarrierBookingService>();
        services.AddScoped<CarrierTrackingService>();
        services.AddScoped<ICarrierRuleService, CarrierRuleService>();
        services.AddScoped<IShippingFeeRuleService, ShippingFeeRuleService>();
        services.AddScoped<ICarrierWebhookService, CarrierWebhookService>();
        services.AddHostedService<CarrierTrackingSyncJob>();
        return services;
    }
}
