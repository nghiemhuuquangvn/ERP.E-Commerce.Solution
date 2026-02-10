using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ERP.Identity.Application.DTOs.Auth;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Infrastructure.Repositories;
using static ERP.Identity.Application.DTOs.Auth.LoginDto;
using static ERP.Identity.Application.DTOs.Auth.RefreshTokenDto;
using static ERP.Identity.Application.DTOs.Auth.RegisterDto;

namespace ERP.Identity.Infrastructure.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ITokenService _jwtService;
        private readonly IRefreshTokenRepository _refreshTokenRepository;
        private readonly IUserRepository _userRepository;
        private readonly IPasswordHasher _passwordHasher;

        public AuthService(IUnitOfWork unitOfWork, ITokenService jwtService, IRefreshTokenRepository refreshTokenRepository, IUserRepository userRepository, IPasswordHasher passwordHasher)
        {
            _unitOfWork = unitOfWork;
            _jwtService = jwtService;
            _refreshTokenRepository = refreshTokenRepository;
            _userRepository = userRepository;
            _passwordHasher = passwordHasher;
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct)
        {
            var tenant = await _unitOfWork.Tenants.GetByIdAsync(request.TenantId, ct)
                ?? throw new UnauthorizedAccessException("Tenant not found");

            var user = await _unitOfWork.Users.GetByEmailAsync(request.Email, tenant.Id, ct);
            if (user == null)
                throw new UnauthorizedAccessException("User not found");

            if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
                throw new UnauthorizedAccessException("Invalid credentials");

            var roles = await _unitOfWork.Roles.GetRolesAsync(user.Id, request.TenantId, ct);

            var permissions = await _unitOfWork.Permissions.GetPermissionAsync(user.Id, request.TenantId, ct);

            var accessToken = await _jwtService.GenerateAccessToken(user, tenant, roles, permissions);

            var refreshToken = await _jwtService.GenerateRefreshToken(user.Id, request.TenantId);

            var refreshTokenEntity = new RefreshToken
            {
                UserId = user.Id,
                Token = refreshToken,
                ExpiredAt = DateTime.UtcNow.AddDays(30),
                CreatedByIp = null

            };

            await _refreshTokenRepository.AddAsync(refreshTokenEntity, ct);

            await _unitOfWork.SaveChangesAsync(ct);

            return new LoginResponse(accessToken, refreshToken);
        }

        public Task<LoginResponse> LoginWithGoogleAsync(ExternalLoginDto.ExternalLoginRequest request, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task<LoginResponse> LoginWithMicrosoftAsync(ExternalLoginDto.ExternalLoginRequest request, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task LogoutAsync(Guid userId, string refreshToken, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public  async Task<RefreshTokenResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default)
        {
            // 1) Hash token & lookup
            var session = await _refreshTokenRepository.GetByTokenAsync(request.RefreshToken);

            if (session is null || !session.IsActive)
                throw new UnauthorizedAccessException("Invalid refresh token");

            // 2) Load context
            var user = await _unitOfWork.Users.GetByIdAsync(session.UserId, ct)
                ?? throw new UnauthorizedAccessException();

            var tenant = await _unitOfWork.Tenants.GetByIdAsync(session.TenantId, ct)
                ?? throw new UnauthorizedAccessException();

            var roles = await _unitOfWork.Roles.GetRolesAsync(user.Id, tenant.Id, ct);

            var permissions = await _unitOfWork.Permissions.GetPermissionAsync(user.Id, tenant.Id, ct);

            // 3) Rotate token (revoke old)
            session.Revoke();

            var newRefreshToken = await _jwtService.GenerateRefreshToken(user.Id, tenant.Id);

            await _unitOfWork.RefreshTokens.AddAsync(
                new RefreshToken
                {
                    UserId = user.Id,
                    TenantId = tenant.Id,
                    Token = newRefreshToken,
                    ExpiredAt = DateTime.UtcNow.AddDays(30),
                    CreatedByIp = request.IpAddress
                }, ct
            );

            // 4) New access token
            var accessToken = await _jwtService.GenerateAccessToken(user, tenant, roles, permissions);

            // 5) Commit
            await _unitOfWork.SaveChangesAsync(ct);

            return new RefreshTokenResponse(accessToken, newRefreshToken);
        }

        public  async Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
        {
            // 1️⃣ Validate tenant
            var tenant = await _unitOfWork.Tenants.GetByIdAsync(request.TenantId, ct)
                ?? throw new InvalidOperationException("Tenant not found");

            if (!tenant.IsActive)
                throw new InvalidOperationException("Tenant is inactive");

            // 2️⃣ Check email duplicate trong tenant
            var existedUser = await _unitOfWork.Users.GetByEmailAsync(request.Email, request.TenantId, ct);

            if (existedUser != null)
                throw new InvalidOperationException("Email already exists");

            // 3️⃣ Hash password
            var passwordHash = _passwordHasher.HashPassword(request.Password);

            // 4️⃣ Create user (DOMAIN)
            var user = User.Create(
                email: request.Email,
                passwordHash: passwordHash
            );

            // 5️⃣ Add user vào tenant (UserTenant / UserRole mặc định)
            user.AssignToTenant(tenant.Id);

            // (optional) gán role mặc định
            user.AssignRole(
                roleCode: SystemRoles.User,
                tenantId: tenant.Id
            );

            // 6️⃣ Persist
            await _userRepository.AddAsync(user, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            // 7️⃣ Response
            return new RegisterResponse(
                Succeeded: true,
                Errors: []
            );
        }
    }

}
