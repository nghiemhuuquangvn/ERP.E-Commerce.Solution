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
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Exceptions;
using ERP.Identity.Infrastructure.Security;

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
        private readonly ILicenseService _licenseService;

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
            IPasswordPolicyService passwordPolicyService,
            ILicenseService licenseService)
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
            _licenseService = licenseService;
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

            // Single source of truth as of the 2026-07-29 permission-architecture
            // consolidation: AuthDB's RolePermissions table now holds every permission
            // code — both IdentityService's own (identity:*/security:*/settings:*/
            // license:*) and the ~230 business-domain codes (crm:*, sales_order:*,
            // payments:*, ...) the other 13 services check via [AuthorizePermission(...)].
            // Those business codes used to exist ONLY as a hardcoded static dictionary
            // (ERP.Shared.Authorization.RoleDefinitions), unioned in here at every login —
            // changing what a role could do required a code change + redeploy of every
            // service referencing the Shared library. Migration
            // src/Database/Identity/013_SeedRoleDefinitionsIntoRolePermissions.sql seeded
            // RolePermissions with that dictionary's exact grants (by RoleCode, across
            // every tenant) so this DB lookup alone reproduces the same permission set —
            // RoleDefinitions.cs is no longer read at runtime, only by that one-time seed.
            var permissionSet = (await EffectivePermissions.ResolveAsync(_unitOfWork, user.Id, tenant.Id, roles, ct)).ToList();

            // Embeds the tenant's currently-licensed module codes into the JWT (parallel to
            // the "permission" claims above) so every other service can check "am I
            // licensed for this tenant" without a network call — see LicenseService.
            // GetEffectiveModulesAsync. Returns [] if the tenant has no valid license.
            var licensedModules = await _licenseService.GetEffectiveModulesAsync(tenant.Id, ct);

            var accessToken = await _jwtService.GenerateAccessToken(user, tenant, roles, permissionSet, licensedModules);
            var refreshToken = await _jwtService.GenerateRefreshToken(user.Id, tenant.Id);

            // Only the hash is persisted — same "opaque high-entropy token, hashed at
            // rest" pattern as PasswordResetToken — so DB read access alone doesn't
            // hand out a usable, long-lived session token. The raw value is returned
            // to the caller below and never stored.
            var refreshTokenEntity = RefreshToken.Create(
                userId: user.Id,
                tenantId: tenant.Id,
                token: OpaqueTokenHasher.Hash(refreshToken),
                expiredAt: DateTime.UtcNow.AddDays(GetConfigInt("Jwt:RefreshTokenExpirationDays", 7)),
                createdByIp: ipAddress
            );

            await _unitOfWork.RefreshTokens.AddAsync(refreshTokenEntity, ct);
            await _sessionService.RegisterSessionAsync(user.Id, tenant.Id, deviceId, deviceName, deviceType, platform, ipAddress, userAgent, ct);

            var userSummary = new UserSummaryDto(user.Id, user.Email, user.FullName, roles, permissionSet);

            return new LoginResponse(accessToken, refreshToken, Succeeded: true, User: userSummary);
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request, string tenantCode, string? ipAddress, string? userAgent, CancellationToken ct)
        {
            // Brute-force guard — checked before any tenant/user lookup so a flood of
            // credential-stuffing attempts against one (ip, email) pair gets rejected
            // cheaply, before a real password hash comparison ever runs.
            var windowMinutes = GetConfigInt("PasswordPolicy:FailedAttemptsWindowMinutes", 15);
            var maxAttempts = GetConfigInt("PasswordPolicy:MaxFailedAttemptsPerWindow", 10);
            var recentFailures = await _unitOfWork.LoginAttempts.CountRecentFailedAsync(
                ipAddress ?? "unknown", request.Email, TimeSpan.FromMinutes(windowMinutes), ct);
            if (recentFailures >= maxAttempts)
                throw new UnauthorizedAccessException("Too many failed login attempts. Please try again later.");

            AuthTenant tenant;
            try
            {
                tenant = await _unitOfWork.Tenants.GetTenantByCodeAsync(tenantCode, ct)
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

            // IsCurrentlyLocked mutates the entity to auto-clear an expired auto-lockout —
            // persist that regardless of which branch runs below, not just on the lock path.
            var wasLocked = user.IsCurrentlyLocked;
            if (wasLocked)
            {
                await _unitOfWork.Users.UpdateAsync(user, ct);
                await _securityAuditService.RecordLoginAttemptAsync(ipAddress ?? "unknown", false, request.Email, user.Id, "Account locked", ct);
                await _securityAuditService.LogAsync("Login", false, tenant.Id, user.Id, request.Email, ipAddress, userAgent, errorReason: "Account locked", ct: ct);
                await _unitOfWork.SaveChangesAsync(ct);
                throw new UnauthorizedAccessException("Account is locked. Please try again later or contact an administrator.");
            }

            if (!_passwordHasher.VerifyPassword(user.PasswordHash, request.Password))
            {
                // Tenant's own configured threshold/duration, falling back to the tenant's
                // defaults (TenantAuthSettings is always created with SystemAuthSettings'
                // values, see CreateDefault) — never the previous hardcoded "5".
                var tenantAuthSettings = await _settingsService.GetOrCreateTenantSettingsEntityAsync(tenant.Id, ct);
                user.RecordLoginFail(tenantAuthSettings.MaxLoginAttempts, tenantAuthSettings.LockoutDurationMinutes);
                await _unitOfWork.Users.UpdateAsync(user, ct);
                await _securityAuditService.RecordLoginAttemptAsync(ipAddress ?? "unknown", false, request.Email, user.Id, "Invalid credentials", ct);
                await _securityAuditService.LogAsync("Login", false, tenant.Id, user.Id, request.Email, ipAddress, userAgent,
                    errorReason: user.IsLocked ? "Invalid credentials — account just locked" : "Invalid credentials", ct: ct);
                await _unitOfWork.SaveChangesAsync(ct);
                throw new UnauthorizedAccessException("Invalid credentials");
            }

            var response = await IssueTokensOrChallengeAsync(user, tenant, ct,
                request.DeviceId, request.DeviceName, request.DeviceType, request.Platform, ipAddress, userAgent);

            await _unitOfWork.SaveChangesAsync(ct);
            return response;
        }

        /// <summary>
        /// Shared 2FA gate for every flow that ends in a successful credential check
        /// (password login, external-provider login, Twitter OAuth callback) — decides
        /// whether to issue tokens immediately or return a RequiresTwoFactor challenge.
        /// Previously only LoginAsync had this check; ExternalLoginAsync/TwitterCallbackAsync
        /// called IssueTokensAsync directly, letting a 2FA-enabled user bypass 2FA entirely
        /// via external login. Does NOT call SaveChangesAsync — callers control the
        /// transaction boundary, same contract as IssueTokensAsync.
        /// </summary>
        private async Task<LoginResponse> IssueTokensOrChallengeAsync(
            User user, AuthTenant tenant, CancellationToken ct,
            string? deviceId = null, string? deviceName = null, string? deviceType = null,
            string? platform = null, string? ipAddress = null, string? userAgent = null,
            string auditEventName = "Login")
        {
            await EnsureAccountCanAuthenticateAsync(user, tenant, auditEventName, ipAddress, userAgent, ct);

            var systemSettings = await _settingsService.GetOrCreateSystemSettingsEntityAsync(ct);
            var tenantSettings = await _settingsService.GetOrCreateTenantSettingsEntityAsync(tenant.Id, ct);

            if (_settingsService.IsTwoFactorRequiredForUser(user, systemSettings, tenantSettings))
            {
                var (challenge, expiryMinutes) = await CreateTwoFactorChallengeAsync(user, tenant.Id, ct);

                await _securityAuditService.RecordLoginAttemptAsync(ipAddress ?? "unknown", true, user.Email, user.Id, ct: ct);
                await _securityAuditService.LogAsync(auditEventName, true, tenant.Id, user.Id, user.Email, ipAddress, userAgent, details: "Password verified, awaiting 2FA", ct: ct);

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

            var response = await IssueTokensAsync(user, tenant, ct, deviceId, deviceName, deviceType, platform, ipAddress, userAgent);

            await _securityAuditService.RecordLoginAttemptAsync(ipAddress ?? "unknown", true, user.Email, user.Id, ct: ct);
            await _securityAuditService.LogAsync(auditEventName, true, tenant.Id, user.Id, user.Email, ipAddress, userAgent, ct: ct);
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

            await EnsureAccountCanAuthenticateAsync(user, tenant, "Login", ipAddress, userAgent, ct);

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
            var isSms = resolvedMethod.Equals("Sms", StringComparison.OrdinalIgnoreCase);
            var recipient = (isSms ? user.PhoneNumber : user.Email)?.Trim();
            if (string.IsNullOrWhiteSpace(recipient))
                throw new BusinessRuleException(
                    "TwoFactorNoDestination",
                    isSms
                        ? "Two-factor authentication by SMS is enabled but the account has no phone number."
                        : "Two-factor authentication by email is enabled but the account has no email address.");
            var destination = MaskDestination(recipient);

            var plaintextCode = _twoFactorCodeGenerator.GenerateCode(codeLength);
            var codeHash = _passwordHasher.HashPassword(plaintextCode);

            var challenge = TwoFactorChallenge.Create(
                userId: user.Id,
                tenantId: tenantId,
                codeHash: codeHash,
                method: resolvedMethod,
                destination: destination,
                recipient: recipient,
                expiryMinutes: expiryMinutes,
                maxAttempts: maxAttempts,
                plaintextCode: plaintextCode);

            await _unitOfWork.TwoFactorChallenges.AddAsync(challenge, ct);

            return (challenge, expiryMinutes);
        }

        public async Task<Guid?> VerifyInteractiveCredentialsAsync(string email, string password, string? ipAddress, string? userAgent, CancellationToken ct)
        {
            var ip = ipAddress ?? "unknown";
            var windowMinutes = GetConfigInt("PasswordPolicy:FailedAttemptsWindowMinutes", 15);
            var maxAttempts = GetConfigInt("PasswordPolicy:MaxFailedAttemptsPerWindow", 10);
            if (await _unitOfWork.LoginAttempts.CountRecentFailedAsync(ip, email, TimeSpan.FromMinutes(windowMinutes), ct) >= maxAttempts)
                return null;

            // No tenant context on this form: try every account sharing the email (one per
            // tenant), skipping deactivated/locked ones exactly like the REST login would.
            var candidates = (await _unitOfWork.Users.FindAllByEmailGloballyAsync(email, ct))
                .Where(u => u.IsActive && !u.IsCurrentlyLocked)
                .ToList();

            foreach (var candidate in candidates)
            {
                if (!_passwordHasher.VerifyPassword(candidate.PasswordHash, password))
                    continue;

                candidate.RecordLoginSuccess();
                await _unitOfWork.Users.UpdateAsync(candidate, ct);
                await _securityAuditService.RecordLoginAttemptAsync(ip, true, email, candidate.Id, ct: ct);
                await _securityAuditService.LogAsync("OidcLogin", true, candidate.TenantId, candidate.Id, email, ipAddress, userAgent, ct: ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return candidate.Id;
            }

            // Count the failure against every matching account so the per-account lockout
            // applies here too (it previously didn't, making this form an unlimited oracle).
            foreach (var candidate in candidates)
            {
                var tenantAuthSettings = await _settingsService.GetOrCreateTenantSettingsEntityAsync(candidate.TenantId, ct);
                candidate.RecordLoginFail(tenantAuthSettings.MaxLoginAttempts, tenantAuthSettings.LockoutDurationMinutes);
                await _unitOfWork.Users.UpdateAsync(candidate, ct);
            }

            await _securityAuditService.RecordLoginAttemptAsync(ip, false, email, null, "Invalid credentials", ct);
            await _securityAuditService.LogAsync("OidcLogin", false, null, null, email, ipAddress, userAgent, errorReason: "Invalid credentials", ct: ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return null;
        }

        public async Task<string?> GetInteractiveLoginDenialAsync(Guid userId, Guid tenantId, CancellationToken ct)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId, ct);
            var tenant = await _unitOfWork.Tenants.GetByIdAsync(tenantId, ct);
            if (user == null || tenant == null)
                return "The account or tenant no longer exists.";
            if (!user.IsActive || user.IsCurrentlyLocked)
                return "The account is deactivated or locked.";
            if (!tenant.IsActive)
                return "The tenant is inactive.";

            var systemSettings = await _settingsService.GetOrCreateSystemSettingsEntityAsync(ct);
            var tenantSettings = await _settingsService.GetOrCreateTenantSettingsEntityAsync(tenant.Id, ct);
            if (_settingsService.IsTwoFactorRequiredForUser(user, systemSettings, tenantSettings))
                return "Two-factor authentication is required for this account and is not supported by this sign-in flow yet.";

            return null;
        }

        /// <summary>
        /// Account-status gate for every flow that is about to issue tokens (password, 2FA,
        /// external, Twitter, refresh). Previously User.IsActive / tenant Status were never
        /// checked, so a deactivated user — or every user of a deactivated tenant — could
        /// still log in and refresh. Audits the rejection before throwing.
        /// </summary>
        private async Task EnsureAccountCanAuthenticateAsync(
            User user, AuthTenant tenant, string auditEventName, string? ipAddress, string? userAgent, CancellationToken ct)
        {
            var reason = !user.IsActive ? "Account deactivated"
                // Admin lock only: a failed-login lockout must not end existing sessions.
                : user.IsAdministrativelyLocked ? "Account locked"
                : !tenant.IsActive ? "Tenant inactive"
                : null;
            if (reason == null)
                return;

            await _securityAuditService.LogAsync(auditEventName, false, tenant.Id, user.Id, user.Email, ipAddress, userAgent, errorReason: reason, ct: ct);
            await _unitOfWork.SaveChangesAsync(ct);
            throw new UnauthorizedAccessException(reason);
        }

        /// <summary>
        /// Dev/test-only escape hatch: when true, forgot-password returns the raw reset token
        /// in its response (no email infrastructure needed to exercise the flow). Program.cs
        /// refuses to start outside Development with this enabled.
        /// </summary>
        private bool ExposeTokensForTesting =>
            bool.TryParse(_configuration["Auth:ExposeTokensInResponseForTesting"], out var expose) && expose;

        private string BuildLink(string configKey, string fallbackBaseUrl, params (string Name, string Value)[] query)
        {
            var baseUrl = _configuration[configKey];
            if (string.IsNullOrWhiteSpace(baseUrl))
                baseUrl = fallbackBaseUrl;

            var queryString = string.Join("&", query.Select(q => $"{q.Name}={Uri.EscapeDataString(q.Value)}"));
            return $"{baseUrl}{(baseUrl.Contains('?') ? '&' : '?')}{queryString}";
        }

        /// <summary>
        /// Issues a single-use email-confirmation token (only its hash is stored) and queues
        /// the confirmation link for delivery via the Outbox → NotificationService. Does NOT
        /// call SaveChangesAsync — callers control the transaction boundary.
        /// </summary>
        private async Task IssueEmailConfirmationAsync(User user, AuthTenant tenant, CancellationToken ct)
        {
            var expiryHours = GetConfigInt("Auth:EmailConfirmationTokenExpiryHours", 48);
            var rawToken = OpaqueTokenHasher.GenerateToken();
            var confirmToken = EmailConfirmToken.Create(user.Id, OpaqueTokenHasher.Hash(rawToken), DateTime.UtcNow.AddHours(expiryHours));
            confirmToken.RaiseRequested(tenant.Id, user.Email,
                BuildLink("Auth:EmailConfirmationUrl", "http://localhost:5173/confirm-email", ("userId", user.Id.ToString()), ("token", rawToken)),
                expiryHours);
            await _unitOfWork.EmailConfirmTokens.AddAsync(confirmToken, ct);
        }

        /// <summary>
        /// ITenantRepository.GetTenantByCodeAsync throws (rather than returning null) when the
        /// code is unknown; anonymous flows that must not reveal whether a tenant exists use this.
        /// </summary>
        private async Task<AuthTenant?> TryGetTenantByCodeAsync(string tenantCode, CancellationToken ct)
        {
            try
            {
                return await _unitOfWork.Tenants.GetTenantByCodeAsync(tenantCode, ct);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
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

        public async Task<LoginResponse> ExternalLoginAsync(ExternalLoginDto.ExternalLoginRequest request, string tenantCode, CancellationToken ct = default)
        {
            var tenant = await _unitOfWork.Tenants.GetTenantByCodeAsync(tenantCode, ct)
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
                emailVerified: externalUser.EmailVerified,
                fullName: externalUser.FullName,
                tenant: tenant,
                ct: ct);

            var response = await IssueTokensOrChallengeAsync(user, tenant, ct, auditEventName: "ExternalLogin");
            await _unitOfWork.SaveChangesAsync(ct);
            return response;
        }

        /// <summary>
        /// Resolves the local user for an external identity within the tenant:
        ///   1. already linked (provider, providerKey, tenant) -> that user;
        ///   2. the provider VERIFIED the email and a user with that email exists in the tenant -> link to it (an
        ///      unverified address is never proof of ownership, so it is refused instead of taking the account over);
        ///   3. otherwise auto-register a new Customer.
        /// Shared by ExternalLoginAsync and the Twitter authorization-code-flow callback (which never verifies emails).
        ///
        /// Auto-provisioned accounts are ALWAYS Customer: this endpoint is anonymous, and it
        /// used to honor the client-supplied ExternalLoginRequest.UserType — so anyone with a
        /// Google/Microsoft account could self-provision as Admin/SuperAdmin of any tenant.
        /// Staff/Admin accounts are created via the admin-gated POST /users/invite (and can
        /// then link SSO); already-linked users are unaffected.
        /// Does NOT call SaveChangesAsync — the caller commits user and link together.
        /// </summary>
        private async Task<User> FindOrCreateExternalUserAsync(
            string provider, string providerKey, string email, bool emailVerified, string? fullName,
            AuthTenant tenant, CancellationToken ct)
        {
            const UserType userType = UserType.Customer;

            var user = await _unitOfWork.Users.GetByExternalProviderAsync(provider, providerKey, tenant.Id, ct);
            if (user != null)
                return user;

            if (string.IsNullOrWhiteSpace(email))
                throw new UnauthorizedAccessException("The external account did not provide an email address.");

            var normalizedEmail = email.Trim().ToLowerInvariant();

            var existing = await _unitOfWork.Users.GetByEmailAsync(normalizedEmail, tenant.Id, ct);
            if (existing != null)
            {
                if (!emailVerified)
                    throw new UnauthorizedAccessException(
                        "An account with this email already exists. Sign in with your password to continue.");

                // Same verified mailbox -> same person. The account's own status (active, locked, tenant) is checked
                // by IssueTokensOrChallengeAsync like for any other sign-in, and 2FA still applies.
                if (existing.IsEmailConfirmed != true)
                    existing.ConfirmEmail();
                await _unitOfWork.ExternalLogins.AddAsync(
                    ExternalLogin.Create(existing.Id, tenant.Id, provider, providerKey, normalizedEmail), ct);
                return existing;
            }

            // The account has no password of its own: a random one nobody knows keeps PasswordHash valid while
            // password sign-in stays impossible (the user can set one through "forgot password").
            var unusablePasswordHash = _passwordHasher.HashPassword(
                Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));

            user = User.Create(
                email: normalizedEmail,
                username: normalizedEmail,
                passwordHash: unusablePasswordHash,
                tenantId: tenant.Id,
                userType: userType
            );
            if (!string.IsNullOrWhiteSpace(fullName))
                user.UpdateProfile(fullName.Trim(), null);
            if (emailVerified)
                user.ConfirmEmail();
            user.AddExternalLogin(provider, providerKey, normalizedEmail, tenant.Id);
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
                emailVerified: false,
                fullName: null,
                tenant: tenant,
                ct: ct);

            var response = await IssueTokensOrChallengeAsync(user, tenant, ct, auditEventName: "TwitterLogin");
            await _unitOfWork.SaveChangesAsync(ct);
            return response;
        }

        public async Task LogoutAsync(Guid userId, string refreshToken, string? jti, DateTime? tokenExpiresAt, string? ipAddress, CancellationToken ct = default)
        {
            await _unitOfWork.RefreshTokens.RevokeAsync(OpaqueTokenHasher.Hash(refreshToken), ct);

            if (!string.IsNullOrWhiteSpace(jti) && tokenExpiresAt.HasValue)
                await _sessionService.BlacklistTokenAsync(jti, userId, tokenExpiresAt.Value, revokedBy: userId.ToString(), ct);

            var user = await _unitOfWork.Users.GetByIdAsync(userId, ct);
            await _securityAuditService.LogAsync("Logout", true, user?.TenantId, userId, user?.Email, ipAddress, ct: ct);

            await _unitOfWork.SaveChangesAsync(ct);
        }

        public async Task<RefreshTokenResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default)
        {
            // A missing/empty token (e.g. a client that never actually had a session)
            // must throw the same intentional exception as every other invalid-token
            // case below — otherwise it falls straight into OpaqueTokenHasher.Hash's
            // incidental ArgumentNullException, which the shared exception middleware
            // deliberately leaves as a 500 (see GlobalExceptionMiddleware.MapException).
            if (string.IsNullOrEmpty(request.RefreshToken))
                throw new UnauthorizedAccessException("Invalid refresh token");

            // 1) Hash token & lookup
            var session = await _unitOfWork.RefreshTokens.GetByTokenAsync(OpaqueTokenHasher.Hash(request.RefreshToken));

            if (session is null)
                throw new UnauthorizedAccessException("Invalid refresh token");

            // Tenant-from-URL binding: the refresh must happen on the session's own tenant host.
            if (request.TenantId == Guid.Empty || session.TenantId != request.TenantId)
                throw new UnauthorizedAccessException("Invalid refresh token");

            if (!session.IsTokenValid)
            {
                // A caller presenting an already-revoked (not merely naturally-expired)
                // refresh token is the classic stolen-refresh-token signal — rotation
                // means the legitimate holder already moved past this exact token, so
                // whoever just presented it again isn't them. Revoke every other active
                // refresh token/device session for this user as a precaution, forcing a
                // full re-login everywhere rather than just rejecting this one request.
                if (session.RevokedAt != null)
                {
                    await _sessionService.RevokeAllForUserAsync(session.UserId, ct);

                    await _securityAuditService.LogAsync("RefreshTokenReuseDetected", false, session.TenantId, session.UserId,
                        errorReason: "Revoked refresh token was presented again — possible token theft; all sessions revoked", ct: ct);
                    await _unitOfWork.SaveChangesAsync(ct);
                }

                throw new UnauthorizedAccessException("Invalid refresh token");
            }

            // 2) Load context
            var user = await _unitOfWork.Users.GetByIdAsync(session.UserId, ct)
                ?? throw new UnauthorizedAccessException();

            var tenant = await _unitOfWork.Tenants.GetByIdAsync(session.TenantId, ct)
                ?? throw new UnauthorizedAccessException();

            // A deactivated user/tenant must not keep minting access tokens off a refresh
            // token issued before the deactivation.
            await EnsureAccountCanAuthenticateAsync(user, tenant, "RefreshToken", request.IpAddress, null, ct);

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

                // 2️⃣ Check email duplicate within THIS tenant only — Users.Email is now unique
                // per (Email, TenantId), not globally (see migration 014), so two independent
                // tenants may legitimately share the same address. An account that already
                // exists in a DIFFERENT tenant must go through POST /api/users/invite
                // (admin-gated) to join this one, not silently self-attach here.
                var existedUser = await _unitOfWork.Users.GetByEmailAsync(request.Email, tenant.Id, ct);

                if (existedUser != null)
                    throw new DuplicateException("Email", request.Email);

                // 3️⃣ Enforce password policy (length/complexity) — previously only
                // ResetPasswordAsync/ChangePasswordAsync validated this, so self-service
                // registration could set an arbitrarily weak password.
                await _passwordPolicyService.ValidateNewPasswordAsync(tenant.Id, Guid.NewGuid(), request.Password, ct);

                // 5️⃣ Hash password
                var passwordHash = _passwordHasher.HashPassword(request.Password.Trim());

                // 6️⃣ Resolve display name: explicit UserName > FirstName+LastName > email
                var userName = request.UserName;
                if (string.IsNullOrWhiteSpace(userName))
                {
                    var combined = $"{request.FirstName} {request.LastName}".Trim();
                    userName = string.IsNullOrWhiteSpace(combined) ? request.Email : combined;
                }

                // 7️⃣ Create user (DOMAIN). Anonymous self-registration is ALWAYS a Customer —
                //    request.UserType used to be honored here, letting anyone register as
                //    Admin/SuperAdmin of any tenant. RegisterRequestValidator also rejects any
                //    other value; Staff/Admin accounts go through POST /users/invite.
                const UserType selfServiceUserType = UserType.Customer;
                var user = User.Create(
                    email: request.Email,
                    username: userName,
                    passwordHash: passwordHash,
                    tenantId: tenant.Id,
                    userType: selfServiceUserType
                );

                await _unitOfWork.Users.AddAsync(user, ct);

                // 8️⃣ Assign to tenant + default role for user type
                user.AssignToTenant(tenant);

                var roleMap = _userRoleResolver.GetRolesForUser(selfServiceUserType);
                var roles = await _unitOfWork.Roles.GetRolesByCodeAsync(roleMap, tenant.Id, ct);
                if (roles == null || !roles.Any())
                    throw new InvalidOperationException($"Default role for user type '{selfServiceUserType}' not found in tenant '{request.TenantCode}'");
                foreach (var role in roles)
                {
                    user.AssignRole(role, tenant.Id);
                }

                // 9️⃣ Persist User + roles to DB before issuing tokens (RefreshTokens/DeviceSessions
                //    FK-reference Users.Id; SaveChanges here ensures the row exists before they insert)
                await _unitOfWork.SaveChangesAsync(ct);

                // Email confirmation is token-based (H1): a single-use link is emailed via
                // NotificationService; POST /auth/confirm-email requires that token.
                await IssueEmailConfirmationAsync(user, tenant, ct);

                // 🔟 Issue tokens so user is logged in immediately after registration
                var loginResponse = await IssueTokensAsync(user, tenant, ct);

                // 1️⃣1️⃣ Audit + commit
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

        public async Task<LoginResponse> SignupTenantAsync(TenantSignupDto.TenantSignupRequest request, CancellationToken ct = default)
        {
            try
            {
                await _unitOfWork.BeginTransactionAsync(ct);

                // 1) Tenant code must not already exist
                if (await _unitOfWork.Tenants.ExistsByCodeAsync(request.TenantCode, ct))
                    throw new DuplicateException("TenantCode", request.TenantCode);

                // 2) Create the tenant. Persist immediately (not just RegisterAsync's
                // single-end-of-flow SaveChanges) — the Role/RolePermission inserts below go
                // through IPermissionRepository.AssignToRoleAsync's raw FK columns rather than
                // a tracked navigation collection, so this keeps the FK insert order
                // unambiguous instead of relying on EF's dependency-graph inference for a
                // relationship shape RegisterAsync never actually exercises.
                var tenant = AuthTenant.Create(request.TenantCode, request.TenantName);
                await _unitOfWork.Tenants.AddAsync(tenant, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                // 3) Seed a generic self-administration Admin role for this new tenant — every
                // Permission whose Module is "Identity"/"System" (manage this tenant's own
                // users/roles/settings), deliberately NOT any ERP business-domain permissions
                // (sales_order:*/crm:*/etc.) — those don't apply to a non-ERP consumer of this
                // service, and an ERP operator can still grant broader roles manually after.
                var role = Role.Create(SystemRoles.Admin, "Administrator", tenant.Id);
                await _unitOfWork.Roles.AddAsync(role, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                // Tenant-scope only: filtering by Module used to include identity:tenants:*,
                // identity:permissions:manage and settings:system:* (all Module "Identity"),
                // so anonymous tenant signup produced an administrator of EVERY tenant.
                var allPermissions = await _unitOfWork.Permissions.ListAllAsync(ct);
                var selfAdminPermissions = allPermissions.Where(p =>
                    (p.Module == "Identity" || p.Module == "System")
                    && PermissionScopes.IsGrantableIn(p.PermissionCode, tenant.Id));
                foreach (var permission in selfAdminPermissions)
                    await _unitOfWork.Permissions.AssignToRoleAsync(role.Id, permission.Id, tenant.Id, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                // 4) Password policy + create the first (Admin) user
                await _passwordPolicyService.ValidateNewPasswordAsync(tenant.Id, Guid.NewGuid(), request.AdminPassword, ct);
                var passwordHash = _passwordHasher.HashPassword(request.AdminPassword.Trim());
                var userName = string.IsNullOrWhiteSpace(request.AdminFullName) ? request.AdminEmail : request.AdminFullName;

                var user = User.Create(
                    email: request.AdminEmail,
                    username: userName,
                    passwordHash: passwordHash,
                    tenantId: tenant.Id,
                    userType: UserType.Admin
                );

                await _unitOfWork.Users.AddAsync(user, ct);
                user.AssignToTenant(tenant);
                user.AssignRole(role, tenant.Id);

                await _unitOfWork.SaveChangesAsync(ct);

                await IssueEmailConfirmationAsync(user, tenant, ct);

                // 5) Issue tokens so the new admin is logged in immediately, same as RegisterAsync
                var loginResponse = await IssueTokensAsync(user, tenant, ct);

                await _securityAuditService.LogAsync("TenantSignup", true, tenant.Id, user.Id, user.Email, ct: ct);
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
            // Identical response whether or not the tenant/account exists (no user
            // enumeration), and the raw token is NEVER returned to the caller — it used to
            // be, which let anyone who knew an email + tenant code reset that account's
            // password (including SuperAdmin). The token is delivered only by email.
            var tenant = await TryGetTenantByCodeAsync(request.TenantCode, ct);
            var user = tenant == null ? null : await _unitOfWork.Users.GetByEmailAsync(request.Email, tenant.Id, ct);
            if (tenant == null || user == null || !user.IsActive || !tenant.IsActive)
                return new ForgotPasswordResponse(Succeeded: true);

            // Only the SHA-256 hash is persisted (see OpaqueTokenHasher); the raw token
            // exists only inside the emailed link.
            var expiryMinutes = GetConfigInt("Auth:PasswordResetTokenExpiryMinutes", 60);
            var resetToken = OpaqueTokenHasher.GenerateToken();
            var passwordResetToken = PasswordResetToken.Create(user.Id, OpaqueTokenHasher.Hash(resetToken), DateTime.UtcNow.AddMinutes(expiryMinutes));
            passwordResetToken.RaiseRequested(tenant.Id, user.Email,
                BuildLink("Auth:PasswordResetUrl", "http://localhost:5173/reset-password", ("token", resetToken)),
                expiryMinutes);

            await _unitOfWork.PasswordResetTokens.AddAsync(passwordResetToken, ct);
            await _securityAuditService.LogAsync("PasswordResetRequested", true, tenant.Id, user.Id, user.Email, ct: ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return new ForgotPasswordResponse(
                Succeeded: true,
                ResetToken: ExposeTokensForTesting ? resetToken : null
            );
        }

        public async Task<ResetPasswordResponse> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Token))
                throw new BusinessRuleException("InvalidResetToken", "Invalid or expired reset token");

            var resetToken = await _unitOfWork.PasswordResetTokens.GetByTokenAsync(OpaqueTokenHasher.Hash(request.Token), ct);
            if (resetToken == null || !resetToken.IsValid)
                throw new BusinessRuleException("InvalidResetToken", "Invalid or expired reset token");

            var user = await _unitOfWork.Users.GetByIdAsync(resetToken.UserId, ct);
            if (user == null || !user.IsActive)
                throw new BusinessRuleException("InvalidResetToken", "Invalid or expired reset token");

            await _passwordPolicyService.ValidateNewPasswordAsync(user.TenantId, user.Id, request.NewPassword, ct);

            var previousHash = user.PasswordHash;
            var newPasswordHash = _passwordHasher.HashPassword(request.NewPassword);
            user.ChangePassword(newPasswordHash);
            resetToken.MarkUsed();

            await _passwordPolicyService.RecordPasswordHistoryAsync(user.Id, previousHash, ct);
            await _unitOfWork.Users.UpdateAsync(user, ct);
            await _unitOfWork.PasswordResetTokens.UpdateAsync(resetToken, ct);

            // A password reset is the recovery path for a compromised account — every
            // existing session (including the attacker's) must stop refreshing.
            await _sessionService.RevokeAllForUserAsync(user.Id, ct);

            await _securityAuditService.LogAsync("PasswordReset", true, user.TenantId, user.Id, user.Email, ct: ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return new ResetPasswordResponse(Succeeded: true);
        }

        public async Task<ConfirmEmailResponse> ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken ct)
        {
            // Requires the single-use token from the emailed link — previously any anonymous
            // caller could confirm any account's email knowing only its UserId.
            if (!Guid.TryParse(request.UserId, out var userId) || string.IsNullOrWhiteSpace(request.Token))
                throw new BusinessRuleException("InvalidConfirmationToken", "Invalid or expired confirmation token");

            var confirmToken = await _unitOfWork.EmailConfirmTokens.GetByTokenAsync(OpaqueTokenHasher.Hash(request.Token), ct);
            if (confirmToken == null || confirmToken.UserId != userId || confirmToken.IsConfirmed || confirmToken.IsExpired)
                throw new BusinessRuleException("InvalidConfirmationToken", "Invalid or expired confirmation token");

            var user = await _unitOfWork.Users.GetByIdAsync(userId, ct);
            if (user == null)
                throw new BusinessRuleException("InvalidConfirmationToken", "Invalid or expired confirmation token");

            confirmToken.Confirm();
            user.ConfirmEmail();
            await _unitOfWork.EmailConfirmTokens.UpdateAsync(confirmToken, ct);
            await _unitOfWork.Users.UpdateAsync(user, ct);
            await _securityAuditService.LogAsync("EmailConfirmed", true, user.TenantId, user.Id, user.Email, ct: ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return new ConfirmEmailResponse(Succeeded: true);
        }
    }

}
