namespace ERP.Shared.Infrastructure.Data;

/// <summary>
/// Supplies the tenant the current unit of work belongs to. Every tenant-scoped DbContext
/// reads it (via <see cref="ITenantScopedDbContext"/>) for both its query filter and the
/// TenantId it stamps on new rows — so a request can neither see nor write another
/// tenant's data. Null means "no tenant known": queries then match nothing (fail closed)
/// and saving a tenant-owned entity throws.
/// </summary>
public interface ITenantProvider
{
    Guid? CurrentTenantId { get; }
}

/// <summary>
/// Explicit tenant for work that has no authenticated user — Kafka/RabbitMQ consumers and
/// background jobs. Wrap the handling of one message in
/// <c>using (TenantScope.Begin(evt.TenantId)) { ... }</c>; it flows across awaits
/// (AsyncLocal) and takes precedence over the HTTP user's tenant.
/// </summary>
public static class TenantScope
{
    private static readonly AsyncLocal<Guid?> CurrentScope = new();

    public static Guid? Current => CurrentScope.Value;

    public static IDisposable Begin(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("A tenant scope requires a non-empty tenant id.", nameof(tenantId));

        var previous = CurrentScope.Value;
        CurrentScope.Value = tenantId;
        return new Restore(previous);
    }

    private sealed class Restore(Guid? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;
            CurrentScope.Value = previous;
            _disposed = true;
        }
    }
}

/// <summary>
/// Default provider: an explicit <see cref="TenantScope"/> wins; otherwise the tenant of the
/// authenticated caller, read through the supplied accessor (typically
/// <c>IUserContextService.GetTenantId()</c> — passed as a delegate so this project stays
/// free of ASP.NET/Authorization dependencies).
/// </summary>
public sealed class TenantProvider : ITenantProvider
{
    private readonly Func<string?> _callerTenantAccessor;

    public TenantProvider(Func<string?> callerTenantAccessor)
    {
        _callerTenantAccessor = callerTenantAccessor;
    }

    public Guid? CurrentTenantId
    {
        get
        {
            if (TenantScope.Current is { } scoped)
                return scoped;

            return Guid.TryParse(_callerTenantAccessor(), out var tenantId) && tenantId != Guid.Empty
                ? tenantId
                : null;
        }
    }
}

public static class TenantProviderServiceCollectionExtensions
{
    /// <summary>
    /// Registers a scoped <see cref="ITenantProvider"/> whose caller tenant comes from
    /// <paramref name="callerTenantAccessor"/>, e.g.
    /// <c>services.AddTenantProvider(sp =&gt; sp.GetRequiredService&lt;IUserContextService&gt;().GetTenantId())</c>.
    /// </summary>
    public static Microsoft.Extensions.DependencyInjection.IServiceCollection AddTenantProvider(
        this Microsoft.Extensions.DependencyInjection.IServiceCollection services,
        Func<IServiceProvider, string?> callerTenantAccessor)
    {
        Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddScoped<ITenantProvider>(
            services, sp => new TenantProvider(() => callerTenantAccessor(sp)));
        return services;
    }
}
