using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ERP.Identity.Domain.Entities;

namespace ERP.Identity.Application.DTOs.Auth
{
    public class RegisterDto
    {
        public record RegisterRequest(string Email, string Password, Guid TenantId, UserType UserType);
        public record RegisterResponse(bool Succeeded = true, IEnumerable<string>? Errors = null);
    }
}
