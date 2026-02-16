using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ERP.Identity.Application.DTOs.Auth
{
    public class ExternalLoginDto
    {
        public sealed record ExternalLoginRequest(string Provider, string ProviderKey, string IdToken, Guid TenantId);
        public sealed record ExternalLoginResponse(string Provider, string ProviderKey, string Email, bool EmailVerified, string? FullName);
    }
}
