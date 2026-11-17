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
        /// <param name="lifetime">How long the token lives (the session policy decides; never above Jwt:Expired). Null = Jwt:Expired.</param>
        Task<string> GenerateAccessToken(User user, AuthTenant tenant, IEnumerable<string> roles, IEnumerable<string> permissions, IEnumerable<string> licensedModules, TimeSpan? lifetime = null);
        Task<string> GenerateRefreshToken(Guid userId, Guid tenantId);
    }
}
