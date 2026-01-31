using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ERP.Identity.Domain.Entities
{
    public class EmailConfirmToken
    {
        public Guid TokenId { get; set; }
        public Guid UserId { get; set; }
        public string? Token { get; set; }
        public DateTime ExpiredAt { get; set; }
        public DateTime ConfirmedAt { get; set; }
    }
}
