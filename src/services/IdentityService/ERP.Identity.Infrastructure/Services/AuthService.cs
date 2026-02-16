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
using ERP.Identity.Application.Interfaces.External;
using ERP.Identity.Infrastructure.ExternalAuthLogin;
using ERP.Identity.Domain.Constants;
using ERP.Identity.Application.Interfaces.Mapping;

namespace ERP.Identity.Infrastructure.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ITokenService _jwtService;
        private readonly IRefreshTokenRepository _refreshTokenRepository;
        private readonly IUserRepository _userRepository;
        private readonly IRoleRepository _roleRepository;
        private readonly IPermissionRepository _permissionRepository;
        private readonly ITenantRepository _tendantRepository;
        private readonly IExternalLoginRepository _externalLoginRepository;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IEnumerable<IExternalAuthProvider> _externalAuthProviders;
        private readonly IUserRoleResolver _userRoleResolver;

        public AuthService(IUnitOfWork unitOfWork, ITokenService jwtService, IRefreshTokenRepository refreshTokenRepository, IUserRepository userRepository,
         IPasswordHasher passwordHasher, IRoleRepository roleRepository, IPermissionRepository permissionRepository, ITenantRepository tenantRepository,
          IEnumerable<IExternalAuthProvider> externalAuthProviders, IExternalLoginRepository externalLoginRepository, IUserRoleResolver userRoleResolver)
        {
            _unitOfWork = unitOfWork;
            _jwtService = jwtService;
            _refreshTokenRepository = refreshTokenRepository;
            _externalLoginRepository = externalLoginRepository;
            _userRoleResolver = userRoleResolver;
            _userRepository = userRepository;
            _roleRepository = roleRepository;
            _permissionRepository = permissionRepository;
            _externalAuthProviders = externalAuthProviders;
            _passwordHasher = passwordHasher;
            _tendantRepository = tenantRepository;

        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct)
        {
            var tenant = await _tendantRepository.GetByIdAsync(request.TenantId, ct)
                ?? throw new UnauthorizedAccessException("Tenant not found");

            var user = await _userRepository.GetByEmailAsync(request.Email, tenant.Id, ct);
            if (user == null)
                throw new UnauthorizedAccessException("User not found");

            if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
                throw new UnauthorizedAccessException("Invalid credentials");

            var roles = await _roleRepository.GetRolesAsync(user.Id, request.TenantId, ct);

            var permissions = await _permissionRepository.GetPermissionAsync(user.Id, request.TenantId, ct);

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

        public async Task<LoginResponse> ExternalLoginAsync(ExternalLoginDto.ExternalLoginRequest request, CancellationToken ct = default)
        {
           var provider = _externalAuthProviders.FirstOrDefault(p => p.Provider.Equals(request.Provider, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("Unsupported external provider");

            var externalUser = await provider.VerifyAsync(request, ct);

            var tenant = await _tendantRepository.GetByIdAsync(request.TenantId, ct)
                ?? throw new UnauthorizedAccessException("Tenant not found");

            var user = await _userRepository.GetByExternalProviderAsync(request.Provider, request.ProviderKey, request.TenantId, ct);

            string accessToken;
            string refreshToken;

            if(user is null)
            {
                // Auto-register user
                user = User.Create(
                    email: externalUser.Email,
                    passwordHash: string.Empty
                );
                user.AddExternalLogin(request.Provider, externalUser.ProviderKey, externalUser.Email);

                user.AssignToTenant(request.TenantId);
                user.AssignRole(SystemRoles.User, request.TenantId);

                await _userRepository.AddAsync(user, ct);

                await _externalLoginRepository.AddAsync(ExternalLogin.Create(
                    userId: user.Id,
                    provider: request.Provider,
                    providerKey: externalUser.ProviderKey,
                    email: externalUser.Email
                ), ct);

                var roles = await _roleRepository.GetRolesAsync(user.Id, request.TenantId, ct);
                var permissions = await _permissionRepository.GetPermissionAsync(user.Id, request.TenantId, ct);

                accessToken = await _jwtService.GenerateAccessToken(user, tenant, roles, permissions);
                refreshToken = await _jwtService.GenerateRefreshToken(user.Id, request.TenantId);

                await _unitOfWork.SaveChangesAsync(ct);
            }
            else
            {
                var roles = await _roleRepository.GetRolesAsync(user.Id, request.TenantId, ct);
                var permissions = await _permissionRepository.GetPermissionAsync(user.Id, request.TenantId, ct);

                accessToken = await _jwtService.GenerateAccessToken(user, tenant, roles, permissions);
                refreshToken = await _jwtService.GenerateRefreshToken(user.Id, request.TenantId);
            }
            return new LoginResponse(accessToken, refreshToken);
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

            var roles = _userRoleResolver.GetRolesForUser(request.UserType);

            foreach(var role in roles)
            {
                user.AssignRole(role, tenant.Id);
            }

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
