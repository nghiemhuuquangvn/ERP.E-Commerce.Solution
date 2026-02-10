using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ERP.Identity.Application.DTOs.Auth.ExternalLoginDto;
using static ERP.Identity.Application.DTOs.Auth.LoginDto;
using static ERP.Identity.Application.DTOs.Auth.RefreshTokenDto;
using static ERP.Identity.Application.DTOs.Auth.RegisterDto;

namespace ERP.Identity.Application.Interfaces.Services
{
    public interface IAuthService
    {
        Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct);
        Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);

        Task<LoginResponse> LoginWithGoogleAsync(ExternalLoginRequest request, CancellationToken ct = default);

        Task<LoginResponse> LoginWithMicrosoftAsync(ExternalLoginRequest request, CancellationToken ct = default);

        Task<RefreshTokenResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default);

        Task LogoutAsync(Guid userId, string refreshToken, CancellationToken ct = default);
    }
}
