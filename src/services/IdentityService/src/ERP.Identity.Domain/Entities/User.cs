using ERP.Identity.Domain.Common;
using ERP.Identity.Domain.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ERP.Identity.Domain.Entities
{
    public class User : BaseEntity
    {
        public string Email { get;  set; } = default!;
        public string? UserName { get; set; }
        public string? FullName { get;  set; } = default!;
        public string PasswordHash { get;  set; } = default!;
        public string? PasswordSalt { get; set; } = default!;
        public bool? IsEmailConfirmed { get; private set; }
        public bool? IsPhoneConfirmed { get; private set; }
        public bool IsActive { get; private set; } = true;
        public bool IsLocked { get; private set; }
        public int AccessFailedCount { get; private set; }
        public DateTime? LastLoginAt { get; private set; }

        /// <summary>
        /// Set only by <see cref="RecordLoginFail"/>'s auto-lockout (null for a manual
        /// admin <see cref="Lock"/>, which stays locked until an explicit <see cref="Unlock"/>).
        /// Callers must check <see cref="IsCurrentlyLocked"/> (which also expires this)
        /// rather than reading <see cref="IsLocked"/> directly, or a lock past its own
        /// expiry will incorrectly still block login.
        /// </summary>
        public DateTime? LockedUntil { get; private set; }

        /// <summary>
        /// True if still locked right now — auto-clears an expired auto-lockout (mutates
        /// state, so callers must persist via UpdateAsync/SaveChangesAsync afterwards if
        /// this returns false after having been true). A manual admin Lock() (LockedUntil
        /// == null) never auto-expires here — only Unlock() clears that one.
        /// </summary>
        public bool IsCurrentlyLocked
        {
            get
            {
                if (!IsLocked)
                    return false;

                if (LockedUntil.HasValue && LockedUntil.Value <= DateTime.UtcNow)
                {
                    IsLocked = false;
                    AccessFailedCount = 0;
                    LockedUntil = null;
                    return false;
                }

                return true;
            }
        }

        public string? PhoneNumber { get; private set; }
        public bool TwoFactorEnabled { get; private set; }
        public string? TwoFactorMethod { get; private set; }

        public Guid TenantId { get; private set; } = default!;

        private readonly List<AuthUserTenant> _authTenants = new();
        public IReadOnlyCollection<AuthUserTenant> UserTenants => _authTenants;
        private readonly List<UserRole> _userRoles = new();
        public IReadOnlyCollection<UserRole> UserRoles => _userRoles.AsReadOnly();
        public ICollection<UserPermission> UserPermissions { get; set; } = new List<UserPermission>();

        private readonly List<ExternalLogin> _externalLogins = new();
        public IReadOnlyCollection<ExternalLogin> ExternalLogins => _externalLogins.AsReadOnly();

        public string UserTypeCode { get; private set; } = default!;

        public User() { }

        public static User Create(string email, string username, string passwordHash, Guid tenantId, UserType userType)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email is required");

            if (string.IsNullOrWhiteSpace(passwordHash))
                throw new ArgumentException("PasswordHash is required");

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email.Trim().ToLowerInvariant(),
                UserName = username.Trim().ToLowerInvariant() ?? email.Trim().ToLowerInvariant(),
                PasswordHash = passwordHash,
                TenantId = tenantId,
                UserTypeCode = userType.ToString(),
                IsActive = true,
                IsEmailConfirmed = false,
                IsPhoneConfirmed = false,
                FullName = username.Trim(),
                CreatedBy = "System",
                CreatedAt = DateTime.UtcNow
            };
            user.AddDomainEvent(new UserRegisteredDomainEvent(user.Id, tenantId, email, userType.ToString()));

            return user;
        }
        public void RecordLoginSuccess()
        {
            AccessFailedCount = 0;
            IsLocked = false;
            LockedUntil = null;
            LastLoginAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Increments the failed-attempt counter and, once it reaches <paramref
        /// name="maxAttempts"/>, locks the account for <paramref
        /// name="lockoutDurationMinutes"/> (auto-expiring — see <see
        /// cref="IsCurrentlyLocked"/> — not a permanent lock like <see cref="Lock"/>).
        /// Callers should pass the caller-tenant's configured
        /// TenantAuthSettings.MaxLoginAttempts/LockoutDurationMinutes (falling back to
        /// SystemAuthSettings' values), not a hardcoded threshold.
        /// </summary>
        public void RecordLoginFail(int maxAttempts, int lockoutDurationMinutes)
        {
            AccessFailedCount++;
            if (AccessFailedCount >= maxAttempts)
            {
                IsLocked = true;
                LockedUntil = DateTime.UtcNow.AddMinutes(lockoutDurationMinutes);
            }
        }
        public void AssignToTenant(AuthTenant tenant)
        {
            if (_authTenants.Any(at => at.TenantId == tenant.Id))
                return;

            var authUserTenant = new AuthUserTenant(this.Id, tenant.Id);
            authUserTenant.User = this;
            authUserTenant.Tenant = tenant;
            _authTenants.Add(authUserTenant);
        }
        public void AssignRole(Role role, Guid tenantId, Guid? changedByUserId = null, string? changedByName = null)
        {
            if(!_authTenants.Any(at => at.TenantId == tenantId))
                throw new InvalidOperationException("User is not assigned to the tenant");

            // A role only ever grants rights inside its own tenant.
            if (role.TenantId != tenantId)
                throw new InvalidOperationException("Role belongs to a different tenant");

            // Previously substituted a fixed, global, hardcoded GUID (RoleIds.FromCode)
            // in place of the real per-tenant Role.Id passed in — those constants never
            // matched any actually-seeded Roles.Id row, so every assignment violated the
            // UserRoles.RoleId FK. Use the real role.Id.
            if (_userRoles.Any(r => r.RoleId == role.Id && r.TenantId == tenantId))
                return;

            var userRole = new UserRole(this.Id, role.Id, tenantId);
            userRole.User = this;
            userRole.Role = role;
            _userRoles.Add(userRole);

            AddDomainEvent(new RoleAssignedDomainEvent(Id, tenantId, role.Id, role.RoleCode, changedByUserId, changedByName));
        }

        public void AddExternalLogin(string provider, string providerKey, string email)
        {
            if (_externalLogins.Any(el => el.Provider == provider && el.ProviderKey == providerKey && el.IsActive))
                return;

            var externalLogin = ExternalLogin.Create(this.Id, provider, providerKey, email);
            _externalLogins.Add(externalLogin);
        }

        public void UpdateProfile(string? fullName, string? userName)
        {
            if (!string.IsNullOrWhiteSpace(fullName))
                FullName = fullName;
            if (!string.IsNullOrWhiteSpace(userName))
                UserName = userName.ToLowerInvariant();

            AddDomainEvent(new UserUpdatedDomainEvent(Id, TenantId, FullName, UserName));
        }

        public void ChangePassword(string newPasswordHash)
        {
            if (string.IsNullOrWhiteSpace(newPasswordHash))
                throw new ArgumentException("Password hash is required");
            PasswordHash = newPasswordHash;
        }

        public void ConfirmEmail()
        {
            IsEmailConfirmed = true;
        }

        public void Deactivate()
        {
            IsActive = false;
        }

        public void Activate()
        {
            IsActive = true;
        }

        /// <summary>Administrative lock: indefinite (no LockedUntil) until <see cref="Unlock"/>.</summary>
        public void Lock()
        {
            IsLocked = true;
            LockedUntil = null;
        }

        /// <summary>
        /// Locked by an administrator (as opposed to the temporary failed-login lockout, which
        /// has a LockedUntil). Only this kind ends existing sessions — otherwise anyone could
        /// log a user out everywhere just by typing wrong passwords for them.
        /// </summary>
        public bool IsAdministrativelyLocked => IsLocked && LockedUntil == null;

        public void Unlock()
        {
            IsLocked = false;
            AccessFailedCount = 0;
            LockedUntil = null;
        }

        public void SetPhoneNumber(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                throw new ArgumentException("Phone number is required");
            PhoneNumber = phone.Trim();
        }

        public void EnableTwoFactor(string method, string? phoneNumber = null)
        {
            if (string.IsNullOrWhiteSpace(method))
                throw new ArgumentException("Two-factor method is required");

            if (method.Equals("Sms", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(phoneNumber))
                SetPhoneNumber(phoneNumber);

            TwoFactorEnabled = true;
            TwoFactorMethod = method;
        }

        public void DisableTwoFactor()
        {
            TwoFactorEnabled = false;
            TwoFactorMethod = null;
        }
    }

}
