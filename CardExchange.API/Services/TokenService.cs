using CardExchange.API.Configuration;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace CardExchange.API.Services
{
    public class TokenService : ITokenService
    {
        private readonly JwtSettings _jwtSettings;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TokenService> _logger;

        public TokenService(
            IOptions<JwtSettings> jwtSettings,
            ApplicationDbContext context,
            ILogger<TokenService> logger)
        {
            _jwtSettings = jwtSettings.Value;
            _context = context;
            _logger = logger;
        }

        public string GenerateAccessToken(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("FirstName", user.FirstName),
                new Claim("LastName", user.LastName)
            };

            // Aggiungi i ruoli dell'utente
            var userRoles = _context.UserRoles
                .Include(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
                .Where(ur => ur.UserId == user.Id && !ur.IsDeleted)
                .ToList();

            foreach (var userRole in userRoles)
            {
                // Aggiungi claim per il ruolo
                claims.Add(new Claim(ClaimTypes.Role, userRole.Role.Name));

                // Aggiungi claim per ogni permesso del ruolo
                foreach (var rolePermission in userRole.Role.RolePermissions)
                {
                    claims.Add(new Claim("Permission", rolePermission.Permission.Name));
                }
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string GenerateRefreshToken()
        {
            var randomNumber = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
        {
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = false,
                ValidateIssuer = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey)),
                ValidateLifetime = false
            };

            var tokenHandler = new JwtSecurityTokenHandler();

            try
            {
                var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);

                if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                    !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                {
                    throw new SecurityTokenException("Invalid token");
                }

                return principal;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante la validazione del token");
                return null;
            }
        }

        public Task<bool> ValidateRefreshToken(User user, string refreshToken)
        {
            if (user.RefreshToken != refreshToken)
                return Task.FromResult(false);

            if (user.RefreshTokenExpiryTime <= DateTime.UtcNow)
                return Task.FromResult(false);

            return Task.FromResult(true);
        }
    }
}