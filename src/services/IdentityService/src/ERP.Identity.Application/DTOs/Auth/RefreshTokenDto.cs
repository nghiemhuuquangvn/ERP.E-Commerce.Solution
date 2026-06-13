using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ERP.Identity.Application.DTOs.Auth
{
    public class RefreshTokenDto
    {
        public record RefreshTokenRequest(string RefreshToken, Guid TenantId, string IpAddress);
        public record RefreshTokenResponse(string AccessToken, string RefreshToken, bool Succeeded = true, IEnumerable<string>? Errors = null);
    }
}
