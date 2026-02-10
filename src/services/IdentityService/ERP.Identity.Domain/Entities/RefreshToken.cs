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

        public void Revoke()
        {
            RevokedAt = DateTime.UtcNow;
        }

        public bool IsActive => RevokedAt == null && DateTime.UtcNow < ExpiredAt;
            
    }
}
