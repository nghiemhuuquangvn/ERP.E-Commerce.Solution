using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ERP.Identity.Application.DTOs.Auth
{
    public class ExternalLoginDto
    {
        public record ExternalLoginRequest(string Provider, string IdToken, Guid TenantId);
        public record ExternalLoginResponse(string AccessToken, string RefreshToken, bool Succeeded = true, IEnumerable<string>? Errors = null);
    }
}
