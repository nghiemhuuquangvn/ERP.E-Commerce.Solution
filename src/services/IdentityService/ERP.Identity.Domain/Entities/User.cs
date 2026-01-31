using ERP.Identity.Domain.Common;
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
        public string PasswordSalt { get; set; }
        public bool IsEmailConfirmed { get; private set; }
        public bool IsPhoneConfirmed { get; private set; }
        public bool IsActive { get; private set; } = true;
        public bool IsLocked { get; private set; }
        public int AccessFailedCount { get; private set; }
        public DateTime? LastLoginAt { get; private set; }

        public ICollection<AuthUserTenant> UserTenants { get; set; } = new List<AuthUserTenant>();
        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
        public ICollection<UserPermission> UserPermissions { get; set; } = new List<UserPermission>();
        public ICollection<ExternalLogin> ExternalLogins { get; set; } = new List<ExternalLogin>();

        public User() { }

        public User(string email, string userName, string fullName, string passwordHash)
        {
            Email = email;
            UserName = userName;
            FullName = fullName;
            PasswordHash = passwordHash;
            IsEmailConfirmed = false;
            IsActive = true;
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
    }

}
