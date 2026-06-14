using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ERP.Identity.Application.DTOs.Auth;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Infrastructure.Repositories;
using static ERP.Identity.Application.DTOs.Auth.LoginDto;
using static ERP.Identity.Application.DTOs.Auth.RefreshTokenDto;
using static ERP.Identity.Application.DTOs.Auth.RegisterDto;
using ERP.Identity.Application.Interfaces.External;
using ERP.Identity.Infrastructure.ExternalAuthLogin;
using ERP.Identity.Domain.Constants;
using ERP.Identity.Application.Interfaces.Mapping;

namespace ERP.Identity.Infrastructure.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ITokenService _jwtService;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IEnumerable<IExternalAuthProvider> _externalAuthProviders;
        private readonly IUserRoleResolver _userRoleResolver;

        public AuthService(IUnitOfWork unitOfWork, ITokenService jwtService, IPasswordHasher passwordHasher,    
          IEnumerable<IExternalAuthProvider> externalAuthProviders, IUserRoleResolver userRoleResolver)
        {
            _unitOfWork = unitOfWork;
            _jwtService = jwtService;
            _userRoleResolver = userRoleResolver;
            _externalAuthProviders = externalAuthProviders;
            _passwordHasher = passwordHasher;

        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct)
        {
            var tenant = await _unitOfWork.Tenants.GetTenantByCodeAsync(request.TenantCode, ct)
                ?? throw new UnauthorizedAccessException("Tenant not found");

            var user = await _unitOfWork.Users.GetByEmailAsync(request.Email, tenant.Id, ct);
            if (user == null)
                throw new UnauthorizedAccessException("User not found");

            if (!_passwordHasher.VerifyPassword(user.PasswordHash, request.Password))
                throw new UnauthorizedAccessException("Invalid credentials");

            var roles = await _unitOfWork.Roles.GetRolesAsync(user.Id, tenant.Id, ct);

            var permissions = await _unitOfWork.Permissions.GetPermissionAsync(user.Id, tenant.Id, ct);

            var accessToken = await _jwtService.GenerateAccessToken(user, tenant, roles, permissions);

            var refreshToken = await _jwtService.GenerateRefreshToken(user.Id, tenant.Id);

            var refreshTokenEntity = RefreshToken.Create(
                userId: user.Id,
                tenantId: tenant.Id,
                token: refreshToken,
                expiredAt: DateTime.UtcNow.AddDays(30),
                createdByIp: null
            );

            await _unitOfWork.RefreshTokens.AddAsync(refreshTokenEntity, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return new LoginResponse(accessToken, refreshToken);
        }

        public async Task<LoginResponse> ExternalLoginAsync(ExternalLoginDto.ExternalLoginRequest request, CancellationToken ct = default)
        {
           var provider = _externalAuthProviders.FirstOrDefault(p => p.Provider.Equals(request.Provider, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("Unsupported external provider");

            var externalUser = await provider.VerifyAsync(request, ct);

            var tenant = await _unitOfWork.Tenants.GetTenantByCodeAsync(request.TenantCode, ct)
                ?? throw new UnauthorizedAccessException("Tenant not found");

            var user = await _unitOfWork.Users.GetByExternalProviderAsync(request.Provider, request.ProviderKey, tenant.Id, ct);

            string accessToken;
            string refreshToken;

            if(user is null)
            {
                // Auto-register user
                user = User.Create(
                    email: externalUser.Email,
                    username: externalUser.Email,
                    passwordHash: string.Empty,
                    tenantId: tenant.Id,
                    userType: request.UserType
                    
                );
                user.AddExternalLogin(request.Provider, externalUser.ProviderKey, externalUser.Email);

                user.AssignToTenant(tenant);

                var urole = _userRoleResolver.GetRolesForUser(request.UserType);
                var tempRoles = await _unitOfWork.Roles.GetRolesByCodeAsync(urole, tenant.Id, ct);
                if(tempRoles == null)
                    throw new InvalidOperationException("Role not found");
                foreach(var role in tempRoles)
                {
                    user.AssignRole(role, tenant.Id);
                }
                

                await _unitOfWork.Users.AddAsync(user, ct);

                var roles = await _unitOfWork.Roles.GetRolesAsync(user.Id, tenant.Id, ct);
                var permissions = await _unitOfWork.Permissions.GetPermissionAsync(user.Id, tenant.Id, ct);

                accessToken = await _jwtService.GenerateAccessToken(user, tenant, roles, permissions);
                refreshToken = await _jwtService.GenerateRefreshToken(user.Id, tenant.Id);

                await _unitOfWork.SaveChangesAsync(ct);
            }
            else
            {
                var roles = await _unitOfWork.Roles.GetRolesAsync(user.Id, tenant.Id, ct);
                var permissions = await _unitOfWork.Permissions.GetPermissionAsync(user.Id, tenant.Id, ct);

                accessToken = await _jwtService.GenerateAccessToken(user, tenant, roles, permissions);
                refreshToken = await _jwtService.GenerateRefreshToken(user.Id, tenant.Id);
            }
            return new LoginResponse(accessToken, refreshToken);
        }

        public async Task LogoutAsync(Guid userId, string refreshToken, CancellationToken ct = default)
        {
            await _unitOfWork.RefreshTokens.RevokeAsync(refreshToken, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        public  async Task<RefreshTokenResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default)
        {
            // 1) Hash token & lookup
            var session = await _unitOfWork.RefreshTokens.GetByTokenAsync(request.RefreshToken);

            if (session is null || !session.IsTokenValid)
                throw new UnauthorizedAccessException("Invalid refresh token");

            // 2) Load context
            var user = await _unitOfWork.Users.GetByIdAsync(session.UserId, ct)
                ?? throw new UnauthorizedAccessException();

            var tenant = await _unitOfWork.Tenants.GetByIdAsync(session.TenantId, ct)
                ?? throw new UnauthorizedAccessException();

            var roles = await _unitOfWork.Roles.GetRolesAsync(user.Id, tenant.Id, ct);

            var permissions = await _unitOfWork.Permissions.GetPermissionAsync(user.Id, tenant.Id, ct);

            // 3) Rotate token (revoke old)
            session.Revoke();

            var newRefreshToken = await _jwtService.GenerateRefreshToken(user.Id, tenant.Id);

            await _unitOfWork.RefreshTokens.AddAsync(
                RefreshToken.Create(
                    userId: user.Id,
                    tenantId: tenant.Id,
                    token: newRefreshToken,
                    expiredAt: DateTime.UtcNow.AddDays(30),
                    createdByIp: request.IpAddress
                ), ct
            );

            // 4) New access token
            var accessToken = await _jwtService.GenerateAccessToken(user, tenant, roles, permissions);

            // 5) Commit
            await _unitOfWork.SaveChangesAsync(ct);

            return new RefreshTokenResponse(accessToken, newRefreshToken);
        }

        public  async Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
        {
            try
            {
                await _unitOfWork.BeginTransactionAsync(ct);
                // 1️⃣ Validate tenant
                var tenant = await _unitOfWork.Tenants.GetTenantByCodeAsync(request.TenantCode, ct)
                    ?? throw new InvalidOperationException("Tenant not found");

                // 2️⃣ Check email duplicate trong tenant
                var existedUser = await _unitOfWork.Users.GetByEmailAsync(request.Email, tenant.Id, ct);

                if (existedUser != null)
                    throw new InvalidOperationException("Email already exists");

                // 3️⃣ Hash password
                var passwordHash = _passwordHasher.HashPassword(request.Password.Trim());

                // 4️⃣ Create user (DOMAIN)
                var user = User.Create(
                    email: request.Email,
                    username: request.UserName ?? request.Email,
                    passwordHash: passwordHash,
                    tenantId: tenant.Id,
                    userType: request.UserType
                );

                await _unitOfWork.Users.AddAsync(user, ct);
                
                // 5️⃣ Add user vào tenant (UserTenant / UserRole mặc định)
                user.AssignToTenant(tenant);

                var roleMap = _userRoleResolver.GetRolesForUser(request.UserType);

                var roles = await _unitOfWork.Roles.GetRolesByCodeAsync(roleMap, tenant.Id,ct);
                if(roles == null)
                    throw new InvalidOperationException("Role not found");
                foreach (var role in roles)
                {
                    user.AssignRole(role, tenant.Id);
                }

                // 6️⃣ Persist
                
                await _unitOfWork.CommitTransactionAsync(ct);

                // 7️⃣ Response
                return new RegisterResponse(
                    Succeeded: true,
                    Errors: []
                );
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync(ct);
                throw;
            }

        }

        public async Task<ForgotPasswordResponse> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct)
        {
            var tenant = await _unitOfWork.Tenants.GetTenantByCodeAsync(request.TenantCode, ct)
                ?? throw new InvalidOperationException("Tenant not found");

            var user = await _unitOfWork.Users.GetByEmailAsync(request.Email, tenant.Id, ct);
            if (user == null)
                throw new InvalidOperationException("User not found");

            // Generate password reset token
            var resetToken = Guid.NewGuid().ToString();
            var passwordResetToken = PasswordResetToken.Create(user.Id, resetToken, DateTime.UtcNow.AddHours(24));
            await _unitOfWork.PasswordResetTokens.AddAsync(passwordResetToken, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            // In production, send email with reset link
            // For now, return the token for testing
            return new ForgotPasswordResponse(
                Succeeded: true,
                ResetToken: resetToken
            );
        }

        public async Task<ResetPasswordResponse> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct)
        {
            var resetToken = await _unitOfWork.PasswordResetTokens.GetByTokenAsync(request.Token, ct);
            if (resetToken == null || !resetToken.IsValid)
                throw new InvalidOperationException("Invalid or expired reset token");

            var user = await _unitOfWork.Users.GetByIdAsync(resetToken.UserId, ct);
            if (user == null)
                throw new InvalidOperationException("User not found");

            var newPasswordHash = _passwordHasher.HashPassword(request.NewPassword);
            user.ChangePassword(newPasswordHash);
            resetToken.MarkUsed();

            await _unitOfWork.Users.UpdateAsync(user, ct);
            await _unitOfWork.PasswordResetTokens.UpdateAsync(resetToken, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return new ResetPasswordResponse(Succeeded: true);
        }

        public async Task<ConfirmEmailResponse> ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken ct)
        {
            if (!Guid.TryParse(request.UserId, out var userId))
                throw new InvalidOperationException("Invalid user ID");

            var user = await _unitOfWork.Users.GetByIdAsync(userId, ct);
            if (user == null)
                throw new InvalidOperationException("User not found");

            // TODO: Implement proper email confirmation token validation
            // For now, just confirm the email
            user.ConfirmEmail();
            await _unitOfWork.Users.UpdateAsync(user, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return new ConfirmEmailResponse(Succeeded: true);
        }
    }

}
