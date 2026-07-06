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
using ERP.Shared.Infrastructure.Exceptions;

namespace ERP.Identity.Infrastructure.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ITokenService _jwtService;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IEnumerable<IExternalAuthProvider> _externalAuthProviders;
        private readonly IUserRoleResolver _userRoleResolver;
        private readonly ISettingsService _settingsService;
        private readonly ITwoFactorCodeGenerator _twoFactorCodeGenerator;
        private readonly TwitterOAuthService _twitterOAuthService;
        private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;
        private readonly ISecurityAuditService _securityAuditService;
        private readonly ISessionService _sessionService;
        private readonly IPasswordPolicyService _passwordPolicyService;

        public AuthService(
            IUnitOfWork unitOfWork,
            ITokenService jwtService,
            IPasswordHasher passwordHasher,
            IEnumerable<IExternalAuthProvider> externalAuthProviders,
            IUserRoleResolver userRoleResolver,
            ISettingsService settingsService,
            ITwoFactorCodeGenerator twoFactorCodeGenerator,
            TwitterOAuthService twitterOAuthService,
            Microsoft.Extensions.Configuration.IConfiguration configuration,
            ISecurityAuditService securityAuditService,
            ISessionService sessionService,
            IPasswordPolicyService passwordPolicyService)
        {
            _unitOfWork = unitOfWork;
            _jwtService = jwtService;
            _userRoleResolver = userRoleResolver;
            _externalAuthProviders = externalAuthProviders;
            _passwordHasher = passwordHasher;
            _settingsService = settingsService;
            _twoFactorCodeGenerator = twoFactorCodeGenerator;
            _twitterOAuthService = twitterOAuthService;
            _configuration = configuration;
            _securityAuditService = securityAuditService;
            _sessionService = sessionService;
            _passwordPolicyService = passwordPolicyService;
        }

        /// <summary>
        /// Shared tail-end of every flow that ends in issuing a JWT + refresh token:
        /// load roles/permissions, generate tokens, persist the refresh token, register
        /// a device session, build response. Does NOT call SaveChangesAsync — callers
        /// control the transaction boundary.
        /// </summary>
        private async Task<LoginResponse> IssueTokensAsync(
            User user, AuthTenant tenant, CancellationToken ct,
            string? deviceId = null, string? deviceName = null, string? deviceType = null,
            string? platform = null, string? ipAddress = null, string? userAgent = null)
        {
            var roles = await _unitOfWork.Roles.GetRolesAsync(user.Id, tenant.Id, ct);
            var permissions = await _unitOfWork.Permissions.GetPermissionAsync(user.Id, tenant.Id, ct);

            var accessToken = await _jwtService.GenerateAccessToken(user, tenant, roles, permissions);
            var refreshToken = await _jwtService.GenerateRefreshToken(user.Id, tenant.Id);

            var refreshTokenEntity = RefreshToken.Create(
                userId: user.Id,
                tenantId: tenant.Id,
                token: refreshToken,
                expiredAt: DateTime.UtcNow.AddDays(30),
                createdByIp: ipAddress
            );

            await _unitOfWork.RefreshTokens.AddAsync(refreshTokenEntity, ct);
            await _sessionService.RegisterSessionAsync(user.Id, tenant.Id, deviceId, deviceName, deviceType, platform, ipAddress, userAgent, ct);

            var userSummary = new UserSummaryDto(user.Id, user.Email, user.FullName, roles, permissions);

            return new LoginResponse(accessToken, refreshToken, Succeeded: true, User: userSummary);
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request, string? ipAddress, string? userAgent, CancellationToken ct)
        {
            AuthTenant tenant;
            try
            {
                tenant = await _unitOfWork.Tenants.GetTenantByCodeAsync(request.TenantCode, ct)
                    ?? throw new UnauthorizedAccessException("Tenant not found");
            }
            catch (UnauthorizedAccessException)
            {
                await _securityAuditService.RecordLoginAttemptAsync(ipAddress ?? "unknown", false, request.Email, null, "Tenant not found", ct);
                await _unitOfWork.SaveChangesAsync(ct);
                throw;
            }

            var user = await _unitOfWork.Users.GetByEmailAsync(request.Email, tenant.Id, ct);
            if (user == null)
            {
                await _securityAuditService.RecordLoginAttemptAsync(ipAddress ?? "unknown", false, request.Email, null, "User not found", ct);
                await _securityAuditService.LogAsync("Login", false, tenant.Id, null, request.Email, ipAddress, userAgent, errorReason: "User not found", ct: ct);
                await _unitOfWork.SaveChangesAsync(ct);
                throw new UnauthorizedAccessException("User not found");
            }

            if (user.IsLocked)
            {
                await _securityAuditService.RecordLoginAttemptAsync(ipAddress ?? "unknown", false, request.Email, user.Id, "Account locked", ct);
                await _securityAuditService.LogAsync("Login", false, tenant.Id, user.Id, request.Email, ipAddress, userAgent, errorReason: "Account locked", ct: ct);
                await _unitOfWork.SaveChangesAsync(ct);
                throw new UnauthorizedAccessException("Account is locked. Contact an administrator to unlock it.");
            }

            if (!_passwordHasher.VerifyPassword(user.PasswordHash, request.Password))
            {
                user.RecordLoginFail();
                await _unitOfWork.Users.UpdateAsync(user, ct);
                await _securityAuditService.RecordLoginAttemptAsync(ipAddress ?? "unknown", false, request.Email, user.Id, "Invalid credentials", ct);
                await _securityAuditService.LogAsync("Login", false, tenant.Id, user.Id, request.Email, ipAddress, userAgent,
                    errorReason: user.IsLocked ? "Invalid credentials — account just locked" : "Invalid credentials", ct: ct);
                await _unitOfWork.SaveChangesAsync(ct);
                throw new UnauthorizedAccessException("Invalid credentials");
            }

            var systemSettings = await _settingsService.GetOrCreateSystemSettingsEntityAsync(ct);
            var tenantSettings = await _settingsService.GetOrCreateTenantSettingsEntityAsync(tenant.Id, ct);

            if (_settingsService.IsTwoFactorRequiredForUser(user, systemSettings, tenantSettings))
            {
                var (challenge, expiryMinutes) = await CreateTwoFactorChallengeAsync(user, tenant.Id, ct);

                await _securityAuditService.RecordLoginAttemptAsync(ipAddress ?? "unknown", true, request.Email, user.Id, ct: ct);
                await _securityAuditService.LogAsync("Login", true, tenant.Id, user.Id, request.Email, ipAddress, userAgent, details: "Password verified, awaiting 2FA", ct: ct);
                await _unitOfWork.SaveChangesAsync(ct);

                return new LoginResponse(
                    AccessToken: null,
                    RefreshToken: null,
                    Succeeded: true,
                    RequiresTwoFactor: true,
                    ChallengeId: challenge.Id,
                    TwoFactorMethod: challenge.Method,
                    ExpiresInSeconds: expiryMinutes * 60);
            }

            user.RecordLoginSuccess();
            await _unitOfWork.Users.UpdateAsync(user, ct);

            var response = await IssueTokensAsync(user, tenant, ct,
                request.DeviceId, request.DeviceName, request.DeviceType, request.Platform, ipAddress, userAgent);

            await _securityAuditService.RecordLoginAttemptAsync(ipAddress ?? "unknown", true, request.Email, user.Id, ct: ct);
            await _securityAuditService.LogAsync("Login", true, tenant.Id, user.Id, request.Email, ipAddress, userAgent, ct: ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return response;
        }

        public async Task<LoginResponse> VerifyTwoFactorAsync(VerifyTwoFactorRequest request, string? ipAddress, string? userAgent, CancellationToken ct)
        {
            var challenge = await _unitOfWork.TwoFactorChallenges.GetByIdAsync(request.ChallengeId, ct);
            if (challenge == null)
                throw new UnauthorizedAccessException("Two-factor challenge not found");

            var isValid = challenge.VerifyCode(_passwordHasher.VerifyPassword, request.Code);
            await _unitOfWork.TwoFactorChallenges.UpdateAsync(challenge, ct);

            if (!isValid)
            {
                await _securityAuditService.LogAsync("Login", false, challenge.TenantId, challenge.UserId, ipAddress: ipAddress, userAgent: userAgent, errorReason: "Invalid 2FA code", ct: ct);
                await _unitOfWork.SaveChangesAsync(ct);
                var remaining = Math.Max(0, challenge.MaxAttempts - challenge.AttemptCount);
                return new LoginResponse(
                    AccessToken: null,
                    RefreshToken: null,
                    Succeeded: false,
                    Errors: new[] { $"Invalid or expired code. {remaining} attempt(s) remaining." });
            }

            challenge.MarkVerified();
            await _unitOfWork.TwoFactorChallenges.UpdateAsync(challenge, ct);

            var user = await _unitOfWork.Users.GetByIdAsync(challenge.UserId, ct)
                ?? throw new UnauthorizedAccessException("User not found");
            var tenant = await _unitOfWork.Tenants.GetByIdAsync(challenge.TenantId, ct)
                ?? throw new UnauthorizedAccessException("Tenant not found");

            user.RecordLoginSuccess();
            await _unitOfWork.Users.UpdateAsync(user, ct);

            var response = await IssueTokensAsync(user, tenant, ct,
                request.DeviceId, request.DeviceName, request.DeviceType, request.Platform, ipAddress, userAgent);

            await _securityAuditService.LogAsync("Login", true, tenant.Id, user.Id, user.Email, ipAddress, userAgent, details: "2FA verified", ct: ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return response;
        }

        public async Task<LoginResponse> ResendTwoFactorAsync(ResendTwoFactorRequest request, CancellationToken ct)
        {
            var existing = await _unitOfWork.TwoFactorChallenges.GetByIdAsync(request.ChallengeId, ct);
            if (existing == null)
                throw new UnauthorizedAccessException("Two-factor challenge not found");

            var cooldownSeconds = GetConfigInt("TwoFactorAuthentication:ResendCooldownSeconds", 60);
            var createdAt = existing.CreatedAt ?? DateTime.UtcNow;
            var secondsSinceCreated = (DateTime.UtcNow - createdAt).TotalSeconds;

            if (secondsSinceCreated < cooldownSeconds)
            {
                var waitSeconds = (int)Math.Ceiling(cooldownSeconds - secondsSinceCreated);
                return new LoginResponse(
                    AccessToken: null,
                    RefreshToken: null,
                    Succeeded: false,
                    Errors: new[] { $"Please wait {waitSeconds} second(s) before requesting a new code." });
            }

            var user = await _unitOfWork.Users.GetByIdAsync(existing.UserId, ct)
                ?? throw new UnauthorizedAccessException("User not found");

            var (challenge, expiryMinutes) = await CreateTwoFactorChallengeAsync(user, existing.TenantId, ct, existing.Method);
            await _unitOfWork.SaveChangesAsync(ct);

            return new LoginResponse(
                AccessToken: null,
                RefreshToken: null,
                Succeeded: true,
                RequiresTwoFactor: true,
                ChallengeId: challenge.Id,
                TwoFactorMethod: challenge.Method,
                ExpiresInSeconds: expiryMinutes * 60);
        }

        private async Task<(TwoFactorChallenge Challenge, int ExpiryMinutes)> CreateTwoFactorChallengeAsync(
            User user, Guid tenantId, CancellationToken ct, string? method = null)
        {
            var codeLength = GetConfigInt("TwoFactorAuthentication:CodeLength", 6);
            var expiryMinutes = GetConfigInt("TwoFactorAuthentication:ExpiryMinutes", 5);
            var maxAttempts = GetConfigInt("TwoFactorAuthentication:MaxAttempts", 3);

            var resolvedMethod = method ?? (!string.IsNullOrWhiteSpace(user.TwoFactorMethod) ? user.TwoFactorMethod! : "Email");
            var destination = resolvedMethod.Equals("Sms", StringComparison.OrdinalIgnoreCase)
                ? MaskDestination(user.PhoneNumber ?? string.Empty)
                : MaskDestination(user.Email);

            var plaintextCode = _twoFactorCodeGenerator.GenerateCode(codeLength);
            var codeHash = _passwordHasher.HashPassword(plaintextCode);

            var challenge = TwoFactorChallenge.Create(
                userId: user.Id,
                tenantId: tenantId,
                codeHash: codeHash,
                method: resolvedMethod,
                destination: destination,
                expiryMinutes: expiryMinutes,
                maxAttempts: maxAttempts,
                plaintextCode: plaintextCode);

            await _unitOfWork.TwoFactorChallenges.AddAsync(challenge, ct);

            return (challenge, expiryMinutes);
        }

        private int GetConfigInt(string key, int fallback)
        {
            var raw = _configuration[key];
            return int.TryParse(raw, out var value) ? value : fallback;
        }

        private static string MaskDestination(string destination)
        {
            if (string.IsNullOrWhiteSpace(destination))
                return destination;

            if (destination.Contains('@'))
            {
                var parts = destination.Split('@');
                var local = parts[0];
                var masked = local.Length <= 2 ? local : local[..2] + new string('*', Math.Max(1, local.Length - 2));
                return $"{masked}@{parts[1]}";
            }

            return destination.Length <= 4
                ? destination
                : new string('*', destination.Length - 4) + destination[^4..];
        }

        public async Task<LoginResponse> ExternalLoginAsync(ExternalLoginDto.ExternalLoginRequest request, CancellationToken ct = default)
        {
            var tenant = await _unitOfWork.Tenants.GetTenantByCodeAsync(request.TenantCode, ct)
                ?? throw new UnauthorizedAccessException("Tenant not found");

            var providerEnabled = await _settingsService.IsProviderEnabledAsync(tenant.Id, request.Provider, ct);
            if (!providerEnabled)
                throw new BusinessRuleException(
                    "ExternalLoginProviderDisabled",
                    $"External login via {request.Provider} is disabled for this tenant.");

            var provider = _externalAuthProviders.FirstOrDefault(p => p.Provider.Equals(request.Provider, StringComparison.OrdinalIgnoreCase))
                 ?? throw new InvalidOperationException("Unsupported external provider");

            var externalUser = await provider.VerifyAsync(request, ct);

            var user = await FindOrCreateExternalUserAsync(
                provider: request.Provider,
                providerKey: externalUser.ProviderKey,
                email: externalUser.Email,
                userType: request.UserType,
                tenant: tenant,
                ct: ct);

            var response = await IssueTokensAsync(user, tenant, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return response;
        }

        /// <summary>
        /// Finds an existing user by (provider, providerKey) within the tenant, or
        /// auto-registers a new one with the default roles for the given user type.
        /// Shared by ExternalLoginAsync and the Twitter authorization-code-flow callback.
        /// </summary>
        private async Task<User> FindOrCreateExternalUserAsync(
            string provider, string providerKey, string email, UserType userType, AuthTenant tenant, CancellationToken ct)
        {
            var user = await _unitOfWork.Users.GetByExternalProviderAsync(provider, providerKey, tenant.Id, ct);
            if (user != null)
                return user;

            user = User.Create(
                email: email,
                username: email,
                passwordHash: string.Empty,
                tenantId: tenant.Id,
                userType: userType
            );
            user.AddExternalLogin(provider, providerKey, email);
            user.AssignToTenant(tenant);

            var roleCodes = _userRoleResolver.GetRolesForUser(userType);
            var roles = await _unitOfWork.Roles.GetRolesByCodeAsync(roleCodes, tenant.Id, ct);
            if (roles == null)
                throw new InvalidOperationException("Role not found");
            foreach (var role in roles)
            {
                user.AssignRole(role, tenant.Id);
            }

            await _unitOfWork.Users.AddAsync(user, ct);

            return user;
        }

        public async Task<TwitterAuthorizeUrlResponse> GetTwitterAuthorizeUrlAsync(string tenantCode, CancellationToken ct)
        {
            var result = await _twitterOAuthService.GetAuthorizeUrlAsync(tenantCode, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return result;
        }

        public async Task<LoginResponse> TwitterCallbackAsync(TwitterCallbackRequest request, CancellationToken ct)
        {
            var twitterUser = await _twitterOAuthService.HandleCallbackAsync(request.Code, request.State, ct);

            var tenant = await _unitOfWork.Tenants.GetTenantByCodeAsync(twitterUser.TenantCode, ct)
                ?? throw new UnauthorizedAccessException("Tenant not found");

            var user = await FindOrCreateExternalUserAsync(
                provider: "Twitter",
                providerKey: twitterUser.ProviderKey,
                email: twitterUser.Email ?? $"{twitterUser.ProviderKey}@twitter.placeholder",
                userType: UserType.Customer,
                tenant: tenant,
                ct: ct);

            var response = await IssueTokensAsync(user, tenant, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return response;
        }

        public async Task LogoutAsync(Guid userId, string refreshToken, string? jti, DateTime? tokenExpiresAt, string? ipAddress, CancellationToken ct = default)
        {
            await _unitOfWork.RefreshTokens.RevokeAsync(refreshToken, ct);

            if (!string.IsNullOrWhiteSpace(jti) && tokenExpiresAt.HasValue)
                await _sessionService.BlacklistTokenAsync(jti, userId, tokenExpiresAt.Value, revokedBy: userId.ToString(), ct);

            var user = await _unitOfWork.Users.GetByIdAsync(userId, ct);
            await _securityAuditService.LogAsync("Logout", true, user?.TenantId, userId, user?.Email, ipAddress, ct: ct);

            await _unitOfWork.SaveChangesAsync(ct);
        }

        public async Task<RefreshTokenResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default)
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

            // 3) Rotate token (revoke old)
            session.Revoke();

            // 4) Issue new tokens (roles/permissions + access/refresh token + persist)
            var issued = await IssueTokensAsync(user, tenant, ct);

            // 5) Commit
            await _unitOfWork.SaveChangesAsync(ct);

            return new RefreshTokenResponse(issued.AccessToken!, issued.RefreshToken!);
        }

        public async Task<LoginResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
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

                // 4️⃣ Resolve display name: explicit UserName > FirstName+LastName > email
                var userName = request.UserName;
                if (string.IsNullOrWhiteSpace(userName))
                {
                    var combined = $"{request.FirstName} {request.LastName}".Trim();
                    userName = string.IsNullOrWhiteSpace(combined) ? request.Email : combined;
                }

                // 5️⃣ Create user (DOMAIN)
                var user = User.Create(
                    email: request.Email,
                    username: userName,
                    passwordHash: passwordHash,
                    tenantId: tenant.Id,
                    userType: request.UserType
                );

                await _unitOfWork.Users.AddAsync(user, ct);

                // 6️⃣ Assign to tenant + default role for user type
                user.AssignToTenant(tenant);

                var roleMap = _userRoleResolver.GetRolesForUser(request.UserType);
                var roles = await _unitOfWork.Roles.GetRolesByCodeAsync(roleMap, tenant.Id, ct);
                if (roles == null || !roles.Any())
                    throw new InvalidOperationException($"Default role for user type '{request.UserType}' not found in tenant '{request.TenantCode}'");
                foreach (var role in roles)
                {
                    user.AssignRole(role, tenant.Id);
                }

                // 7️⃣ Persist User + roles to DB before issuing tokens (RefreshTokens/DeviceSessions
                //    FK-reference Users.Id; SaveChanges here ensures the row exists before they insert)
                await _unitOfWork.SaveChangesAsync(ct);

                // 8️⃣ Issue tokens so user is logged in immediately after registration
                var loginResponse = await IssueTokensAsync(user, tenant, ct);

                // 9️⃣ Audit + commit
                await _securityAuditService.LogAsync("Register", true, tenant.Id, user.Id, user.Email, ct: ct);
                await _unitOfWork.CommitTransactionAsync(ct);

                return loginResponse;
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

            await _passwordPolicyService.ValidateNewPasswordAsync(user.Id, request.NewPassword, ct);

            var previousHash = user.PasswordHash;
            var newPasswordHash = _passwordHasher.HashPassword(request.NewPassword);
            user.ChangePassword(newPasswordHash);
            resetToken.MarkUsed();

            await _passwordPolicyService.RecordPasswordHistoryAsync(user.Id, previousHash, ct);
            await _unitOfWork.Users.UpdateAsync(user, ct);
            await _unitOfWork.PasswordResetTokens.UpdateAsync(resetToken, ct);
            await _securityAuditService.LogAsync("PasswordReset", true, user.TenantId, user.Id, user.Email, ct: ct);
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
