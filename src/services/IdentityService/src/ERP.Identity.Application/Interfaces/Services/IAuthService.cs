using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ERP.Identity.Application.DTOs.Auth;
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

        Task<LoginResponse> ExternalLoginAsync(ExternalLoginRequest request, CancellationToken ct = default);

        Task<RefreshTokenResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default);

        Task LogoutAsync(Guid userId, string refreshToken, CancellationToken ct = default);

        Task<ForgotPasswordResponse> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct);

        Task<ResetPasswordResponse> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct);

        Task<ConfirmEmailResponse> ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken ct);

        Task<LoginResponse> VerifyTwoFactorAsync(VerifyTwoFactorRequest request, CancellationToken ct);

        Task<LoginResponse> ResendTwoFactorAsync(ResendTwoFactorRequest request, CancellationToken ct);

        Task<TwitterAuthorizeUrlResponse> GetTwitterAuthorizeUrlAsync(string tenantCode, CancellationToken ct);

        Task<LoginResponse> TwitterCallbackAsync(TwitterCallbackRequest request, CancellationToken ct);
    }
}
