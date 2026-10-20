using System.Threading;
using System.Threading.Tasks;
using ERP.Entity.Application.Interfaces.Repositories;
using ERP.Identity.Domain.Entities;

namespace ERP.Identity.Application.Interfaces.Repositories
{
    public interface IEmailConfirmTokenRepository : IGenericRepository<EmailConfirmToken>
    {
        /// <summary>Looks up by the SHA-256 hash of the raw token (see OpaqueTokenHasher).</summary>
        Task<EmailConfirmToken?> GetByTokenAsync(string tokenHash, CancellationToken ct);
    }
}
