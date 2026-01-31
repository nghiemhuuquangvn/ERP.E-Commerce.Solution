using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ERP.Identity.Domain.Events
{
    public class UserRegisteredDomainEvent
    {
        public record UserRegisteredDomainEvent(Guid UserId, string Email);
    }
}
