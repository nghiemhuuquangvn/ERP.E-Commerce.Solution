using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ERP.Identity.Application.DTOs.Auth
{
    public class LoginDto
    {
        public record LoginRequest(
            string Email, string Password, UserType UserType,
            string? DeviceId = null, string? DeviceName = null, string? DeviceType = null, string? Platform = null);

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
