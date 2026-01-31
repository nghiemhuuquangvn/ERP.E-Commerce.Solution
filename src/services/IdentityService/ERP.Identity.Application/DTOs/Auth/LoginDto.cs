using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ERP.Identity.Application.DTOs.Auth
{
    public class LoginDto
    {
        public record LoginRequest(string Email, string Password, Guid TenantId);
        public record LoginResponse(string AccessToken, string RefreshToken);
    }
}
