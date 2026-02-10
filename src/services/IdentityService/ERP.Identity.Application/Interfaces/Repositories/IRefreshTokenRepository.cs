using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ERP.Entity.Application.Interfaces.Repositories;
using ERP.Identity.Domain.Entities;

namespace ERP.Identity.Application.Interfaces.Repositories
{
    public interface IRefreshTokenRepository : IGenericRepository<RefreshToken>
    {
        Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);
        Task<IEnumerable<RefreshToken>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
        Task RevokeAsync(string token, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(string token, CancellationToken cancellationToken = default);
    }
}