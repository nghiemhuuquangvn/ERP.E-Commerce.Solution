using ERP.MFG.Application.Abstractions;
using ERP.MFG.Application.Behaviors;
using ERP.MFG.Application.Services;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.MFG.Application;

public static class DependencyInjection
{
    /// <summary>MediatR handlers, validators and the validation pipeline of the Manufacturing service.</summary>
    public static IServiceCollection AddManufacturingApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<ValidationMarker>());
        services.AddValidatorsFromAssemblyContaining<ValidationMarker>();
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped<ICapacityService, CapacityService>();
        services.AddScoped<ILaborRateResolver, LaborRateResolver>();
        services.AddScoped<IBomResolver, BomResolver>();
        services.AddScoped<IBomSnapshotFactory, BomSnapshotFactory>();
        services.AddScoped<IBomExplosionService, BomExplosionService>();
        return services;
    }
}

/// <summary>Anchor type: the assembly of the application layer.</summary>
public sealed class ValidationMarker;
