using ERP.Identity.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ERP.Identity.Domain.Entities
{
    public class RefreshToken : BaseEntity
    {
        public Guid UserId { get; set; }
        public Guid TenantId { get; set; }
        public string? Token { get; set; }
        public DateTime ExpiredAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public string? CreatedByIp { get; set; }
        public bool IsActive { get; set; }

        public RefreshToken()
        {
            IsActive = true;
        }

        public static RefreshToken Create(Guid userId, Guid tenantId, string token, DateTime expiredAt, string? createdByIp = null)
        {
            var refreshToken = new RefreshToken
            {
                UserId = userId,
                TenantId = tenantId,
                Token = token,
                ExpiredAt = expiredAt,
                CreatedByIp = createdByIp,
                IsActive = true
            };
            return refreshToken;
        }

        public void Revoke()
        {
            RevokedAt = DateTime.UtcNow;
            IsActive = false;
        }

        public bool IsTokenValid => RevokedAt == null && DateTime.UtcNow < ExpiredAt;
            
    }
}
