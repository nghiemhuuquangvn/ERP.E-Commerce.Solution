using System.ComponentModel.DataAnnotations;

namespace ERP.Identity.Application.DTOs.Auth
{
    public class RevokeDto
    {
        public record RevokeTokenRequest([Required] string RefreshToken, Guid TenantId);
        public record RevokeTokenResponse(bool Succeeded = true, IEnumerable<string>? Errors = null);
    }
}