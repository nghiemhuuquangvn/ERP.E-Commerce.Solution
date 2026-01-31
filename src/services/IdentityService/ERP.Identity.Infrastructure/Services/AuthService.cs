using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ERP.Identity.Infrastructure.Services
{
    using ERP.Identity.Application.Interfaces.Services;
    using Identity.Application.DTOs.Auth;
    using Identity.Application.Interfaces.Repositories;
    using Identity.Application.Interfaces.Services;
    using static ERP.Identity.Application.DTOs.Auth.LoginDto;

    namespace Identity.Application.Services;

    public class AuthService : IAuthService
    {
        private readonly IUnitOfWork _uow;
        private readonly IJwtTokenService _jwt;

        public AuthService(IUnitOfWork uow, IJwtTokenService jwt)
        {
            _uow = uow;
            _jwt = jwt;
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct)
        {
            var user = await _uow.Users.GetByEmailAsync(request.Email, ct);
            if (user == null)
                throw new UnauthorizedAccessException();

            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                throw new UnauthorizedAccessException();

            var roles = await _uow.Roles.GetByUserIdAsync(user.Id, ct);
            var roleIds = roles.Select(r => r.Id);

            var permissions = await _uow.Permissions.GetByRoleIdsAsync(roleIds, ct);

            var token = _jwt.GenerateToken(user.Id, roles.Select(r => r.Code), permissions);

            return new LoginResponse(token);
        }
    }

}
