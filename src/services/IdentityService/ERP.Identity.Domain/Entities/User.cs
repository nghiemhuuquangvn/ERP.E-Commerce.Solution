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
        public string Email { get; private set; } = default!;
        public string UserName { get; private set; } = default!;
        public string FullName { get; private set; } = default!;
        public string PasswordHash { get; private set; } = default!;
        public string PasswordSalt { get; set; } = default!;
        public bool IsEmailConfirmed { get; private set; }
        public bool IsPhoneConfirmed { get; private set; }
        public bool IsActive { get; private set; } = true;
        public bool IsLocked { get; private set; }
        public int AccessFailedCount { get; private set; }
        public DateTime? LastLoginAt { get; private set; }

        public Guid TenantId { get; private set; } = default!;

        private readonly List<AuthUserTenant> _authTenants = new();
        public IReadOnlyCollection<AuthUserTenant> UserTenants => _authTenants;
        private readonly List<UserRole> _userRoles = new();
        public IReadOnlyCollection<UserRole> UserRoles => _userRoles.AsReadOnly();
        public ICollection<Role> Roles { get; set; } = new List<Role>();
        public ICollection<UserPermission> UserPermissions { get; set; } = new List<UserPermission>();

        private readonly List<ExternalLogin> _externalLogins = new();
        public IReadOnlyCollection<ExternalLogin> ExternalLogins => _externalLogins.AsReadOnly();

        public User() { }

        public static User Create(string email, string passwordHash)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email is required");

            if (string.IsNullOrWhiteSpace(passwordHash))
                throw new ArgumentException("PasswordHash is required");

            return new User
            {
                Id = Guid.NewGuid(),
                Email = email.Trim().ToLowerInvariant(),
                PasswordHash = passwordHash,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
        }
        public static User Register(string email, string passwordHash, Guid tenantId)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email is required");

            if (string.IsNullOrWhiteSpace(passwordHash))
                throw new ArgumentException("PasswordHash is required");

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email.Trim().ToLowerInvariant(),
                PasswordHash = passwordHash,
                TenantId = tenantId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            user.AddDomainEvent(new UserRegisteredDomainEvent(user.Id, tenantId, email));

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
        public void AssignToTenant(Guid tenantId)
        {
            if (_authTenants.Any(at => at.TenantId == tenantId))
                return;

            var authUserTenant = new AuthUserTenant(this.Id, tenantId);
            _authTenants.Add(authUserTenant);
        }
        public void AssignRole(string roleCode, Guid tenantId)
        {
            if(!_authTenants.Any(at => at.TenantId == tenantId && at.IsActive))
                throw new InvalidOperationException("User is not assigned to the tenant");

            var role = Role.CreateReference(roleCode, tenantId);

            if (_userRoles.Any(r => r.RoleId == role.Id && r.TenantId == tenantId && r.IsActive))
                return;

            var userRole = new UserRole(this.Id, role.Id, tenantId);
            _userRoles.Add(userRole);
        }

        public void AddExternalLogin(string provider, string providerKey, string email)
        {
            if (_externalLogins.Any(el => el.Provider == provider && el.ProviderKey == providerKey && el.IsActive))
                return;

            var externalLogin = ExternalLogin.Create(this.Id, provider, providerKey, email);
            _externalLogins.Add(externalLogin);
        }
    }

}
