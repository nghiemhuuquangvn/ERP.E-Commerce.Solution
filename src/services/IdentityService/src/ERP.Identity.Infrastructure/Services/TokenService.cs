using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ERP.Identity.Infrastructure.Services
{
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;
        private IRefreshTokenRepository _refreshTokenRepository;

        public TokenService(IConfiguration configuration, IRefreshTokenRepository refreshTokenRepository)
        {
            _configuration = configuration;
            _refreshTokenRepository = refreshTokenRepository;
        }
        public Task<string> GenerateAccessToken(User user, AuthTenant tenant, IEnumerable<string> roles, IEnumerable<string> permissions)
        {
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim("tenant_id", tenant.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Name, user.FullName ?? user.UserName ?? user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
            claims.AddRange(permissions.Select(p => new Claim("permission", p)));

            var keyString = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key configuration is missing.");
            var issuer = _configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer configuration is missing.");
            var audience = _configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience configuration is missing.");
            var expirationString = _configuration["Jwt:Expired"] ?? throw new InvalidOperationException("Jwt:AccessTokenExpirationMinutes configuration is missing.");

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(keyString)
            );

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(
                    int.Parse(expirationString)
                ),
                signingCredentials: creds
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
            return Task.FromResult(tokenString);
        }

        public Task<string> GenerateRefreshToken(Guid userId, Guid tenantId)
        {
            var randomBytes = RandomNumberGenerator.GetBytes(64);
            var refreshToken = Convert.ToBase64String(randomBytes);
            return Task.FromResult(refreshToken);
        }

        
    }
}
