using ERP.Identity.Domain.Entities;

namespace ERP.Identity.Application.Interfaces.Repositories
{
    /// <summary>
    /// Not IGenericRepository&lt;T&gt; — TokenBlacklist's primary key is the JWT's own
    /// "jti" claim (a string), not a surrogate Guid Id like every other entity here.
    /// </summary>
    public interface ITokenBlacklistRepository
    {
        Task AddAsync(TokenBlacklistEntry entry, CancellationToken ct);
        Task<bool> IsBlacklistedAsync(string jti, CancellationToken ct);
    }
}
