using CardExchange.API.Configuration;
using CardExchange.API.Services;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace CardExchange.Tests.Services;

public class TokenServiceTests : IDisposable
{
    private readonly TokenService _tokenService;
    private readonly JwtSettings _jwtSettings;
    private readonly ApplicationDbContext _context;

    public TokenServiceTests()
    {
        _jwtSettings = new JwtSettings
        {
            SecretKey = "TestSecretKeyForUnitTesting123456!@#VeryLong",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            AccessTokenExpirationMinutes = 60,
            RefreshTokenExpirationDays = 7
        };

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _tokenService = new TokenService(
            Options.Create(_jwtSettings),
            _context,
            new Mock<ILogger<TokenService>>().Object
        );
    }

    [Fact]
    public void GenerateAccessToken_ReturnsValidJwt()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            Username = "testuser",
            FirstName = "Test",
            LastName = "User",
            PasswordHash = "hash"
        };

        // Act
        var token = _tokenService.GenerateAccessToken(user);

        // Assert
        token.Should().NotBeNullOrEmpty();

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        jwtToken.Issuer.Should().Be("TestIssuer");
        jwtToken.Audiences.Should().Contain("TestAudience");
    }

    [Fact]
    public void GenerateAccessToken_ContainsRequiredClaims()
    {
        // Arrange
        var user = new User
        {
            Id = 42,
            Email = "claims@test.com",
            Username = "claimsuser",
            FirstName = "Claims",
            LastName = "Test",
            PasswordHash = "hash"
        };

        // Act
        var token = _tokenService.GenerateAccessToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        jwtToken.Claims.Should().Contain(c =>
            c.Type == ClaimTypes.NameIdentifier && c.Value == "42");
        jwtToken.Claims.Should().Contain(c =>
            c.Type == ClaimTypes.Name && c.Value == "claimsuser");
        jwtToken.Claims.Should().Contain(c =>
            c.Type == ClaimTypes.Email && c.Value == "claims@test.com");
        jwtToken.Claims.Should().Contain(c =>
            c.Type == JwtRegisteredClaimNames.Jti);
    }

    [Fact]
    public void GenerateRefreshToken_ReturnsUniqueTokens()
    {
        // Act
        var token1 = _tokenService.GenerateRefreshToken();
        var token2 = _tokenService.GenerateRefreshToken();

        // Assert
        token1.Should().NotBeNullOrEmpty();
        token2.Should().NotBeNullOrEmpty();
        token1.Should().NotBe(token2);
    }

    [Fact]
    public void GenerateRefreshToken_ReturnsBase64String()
    {
        // Act
        var token = _tokenService.GenerateRefreshToken();

        // Assert
        var bytes = Convert.FromBase64String(token);
        bytes.Should().HaveCount(64); // 64 bytes come nell'implementazione
    }

    [Fact]
    public void GetPrincipalFromExpiredToken_WithValidToken_ReturnsPrincipal()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            Username = "testuser",
            FirstName = "Test",
            LastName = "User",
            PasswordHash = "hash"
        };
        var token = _tokenService.GenerateAccessToken(user);

        // Act
        var principal = _tokenService.GetPrincipalFromExpiredToken(token);

        // Assert
        principal.Should().NotBeNull();
        var userId = principal!.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        userId.Should().Be("1");
    }

    [Fact]
    public void GetPrincipalFromExpiredToken_WithInvalidToken_ReturnsNull()
    {
        // Act
        var principal = _tokenService.GetPrincipalFromExpiredToken("invalid-token-string");

        // Assert
        principal.Should().BeNull();
    }

    [Fact]
    public async Task ValidateRefreshToken_WithCorrectToken_ReturnsTrue()
    {
        // Arrange
        var user = new User
        {
            RefreshToken = "valid-refresh-token",
            RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7)
        };

        // Act
        var isValid = await _tokenService.ValidateRefreshToken(user, "valid-refresh-token");

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateRefreshToken_WithWrongToken_ReturnsFalse()
    {
        // Arrange
        var user = new User
        {
            RefreshToken = "correct-token",
            RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7)
        };

        // Act
        var isValid = await _tokenService.ValidateRefreshToken(user, "wrong-token");

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateRefreshToken_WithExpiredToken_ReturnsFalse()
    {
        // Arrange
        var user = new User
        {
            RefreshToken = "expired-token",
            RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(-1) // Scaduto
        };

        // Act
        var isValid = await _tokenService.ValidateRefreshToken(user, "expired-token");

        // Assert
        isValid.Should().BeFalse();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
