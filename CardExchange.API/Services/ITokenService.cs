using CardExchange.Core.Entities;
using System.Security.Claims;

namespace CardExchange.API.Services
{
    public interface ITokenService
    {
        string GenerateAccessToken(User user);
        string GenerateRefreshToken();
        ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
        Task<bool> ValidateRefreshToken(User user, string refreshToken);
    }
}