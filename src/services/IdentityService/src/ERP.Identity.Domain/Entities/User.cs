using ERP.Identity.Domain.Common;
using ERP.Identity.Domain.Constants;
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
            LastLoginAt = DateTime.UtcNow;
        }

        public void RecordLoginFail()
        {
            AccessFailedCount++;
            if (AccessFailedCount >= 5)
                IsLocked = true;
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
        public void AssignRole(Role role, Guid tenantId)
        {
            if(!_authTenants.Any(at => at.TenantId == tenantId))
                throw new InvalidOperationException("User is not assigned to the tenant");

            //var role = Role.CreateReference(roleCode, tenantId);
            var roleId = RoleIds.FromCode(role.RoleCode);

            if (_userRoles.Any(r => r.RoleId == roleId && r.TenantId == tenantId))
                return;

            var userRole = new UserRole(this.Id, roleId, tenantId);
            userRole.User = this;
            userRole.Role = role;
            _userRoles.Add(userRole);

            AddDomainEvent(new RoleAssignedDomainEvent(Id, tenantId, roleId, role.RoleCode));
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

        public void Lock()
        {
            IsLocked = true;
        }

        public void Unlock()
        {
            IsLocked = false;
            AccessFailedCount = 0;
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
