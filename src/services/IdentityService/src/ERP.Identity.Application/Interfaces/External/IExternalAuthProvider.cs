using ERP.Identity.Application.DTOs.Auth;

namespace ERP.Identity.Application.Interfaces.External
{
    public interface IExternalAuthProvider
    {
        string Provider { get; }
        Task<ExternalLoginDto.ExternalLoginResponse> VerifyAsync(
            ExternalLoginDto.ExternalLoginRequest request, CancellationToken cancellationToken = default);
    }
}