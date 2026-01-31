using ERP.Identity.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ERP.Identity.Application.Interfaces.Services
{
    public interface ITokenService
    {
        Task<string> GenerateAccessToken(User user, AuthTenant tenant, IEnumerable<string> roles, IEnumerable<string> permissions);
        Task<string> GenerateRefreshToken(Guid userId, Guid tenantId);
    }
}
