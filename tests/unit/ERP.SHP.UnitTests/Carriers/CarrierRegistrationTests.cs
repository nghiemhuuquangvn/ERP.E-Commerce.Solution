using ERP.Shared.Infrastructure.Caching;
using ERP.Shared.Infrastructure.Data;
using ERP.SHP.Application.Interfaces;
using ERP.SHP.Application.Services;
using ERP.SHP.Infrastructure;
using ERP.SHP.Infrastructure.BackgroundJobs;
using ERP.SHP.Infrastructure.Persistence;
using ERP.SHP.Infrastructure.Repositories;
using ERP.SHP.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ERP.SHP.UnitTests.Carriers;

/// <summary>
/// A missing registration only shows at the first request (the live check of P1-11 found one after every unit test had passed): this resolves
/// everything the carrier integration needs from the same extension the service uses.
/// </summary>
public class CarrierRegistrationTests
{
    [Fact]
    public void Everything_the_carrier_integration_needs_can_be_resolved()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Security:SettingsEncryptionKey"] = "test-key" }).Build());
        services.AddDbContext<ShippingDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddSingleton<ITenantProvider>(new TenantProvider(() => Guid.NewGuid().ToString()));
        services.AddSingleton(new Mock<ICacheService>().Object);
        services.AddScoped<IShipmentRepository, ShipmentRepository>();
        services.AddScoped<ICarrierRepository, CarrierRepository>();
        services.AddScoped<IReturnShipmentRepository, ReturnShipmentRepository>();
        services.AddScoped<IShippingUnitOfWork, ShippingUnitOfWork>();
        services.AddScoped<IShippingSettingsService, ShippingSettingsService>();
        services.AddCarrierIntegration();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = false });
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        sp.GetServices<ICarrierProvider>().Select(p => p.Code).Should().BeEquivalentTo("GHTK", "GHN", "VTP", "JNT");
        sp.GetRequiredService<ICarrierProviderResolver>().Should().NotBeNull();
        sp.GetRequiredService<CarrierBookingService>().Should().NotBeNull();
        sp.GetRequiredService<CarrierTrackingService>().Should().NotBeNull();
        sp.GetRequiredService<ICarrierRuleService>().Should().NotBeNull();
        sp.GetRequiredService<ICarrierWebhookService>().Should().NotBeNull();
        sp.GetRequiredService<IShippingSettingsService>().Should().NotBeNull();
        provider.GetServices<IHostedService>().Should().Contain(s => s is CarrierTrackingSyncJob);
    }
}
