using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ERP.Identity.Application.DTOs.Auth
{
    public class LoginDto
    {
        public record LoginRequest(string Email, string Password, string TenantCode, UserType UserType);

        public record UserSummaryDto(Guid Id, string Email, string? FullName, IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions);

        public record LoginResponse(
            string? AccessToken,
            string? RefreshToken,
            bool Succeeded = true,
            IEnumerable<string>? Errors = null,
            bool RequiresTwoFactor = false,
            Guid? ChallengeId = null,
            string? TwoFactorMethod = null,
            int? ExpiresInSeconds = null,
            UserSummaryDto? User = null);
    }
}
