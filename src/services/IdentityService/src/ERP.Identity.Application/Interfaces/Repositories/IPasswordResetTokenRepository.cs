using System;
using System.Threading;
using System.Threading.Tasks;
using ERP.Entity.Application.Interfaces.Repositories;
using ERP.Identity.Domain.Entities;

namespace ERP.Identity.Application.Interfaces.Repositories
{
    public interface IPasswordResetTokenRepository : IGenericRepository<PasswordResetToken>
    {
        Task<PasswordResetToken?> GetByTokenAsync(string token, CancellationToken ct);
    }
}
