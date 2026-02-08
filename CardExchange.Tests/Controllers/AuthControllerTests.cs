using CardExchange.API.Configuration;
using CardExchange.API.Controllers;
using CardExchange.API.DTOs.Requests;
using CardExchange.API.DTOs.Responses;
using CardExchange.API.Services;
using CardExchange.Core.Entities;
using CardExchange.Core.Interfaces;
using CardExchange.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Claims;

namespace CardExchange.Tests.Controllers;

public class AuthControllerTests
{
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly Mock<ILogger<AuthController>> _loggerMock;
    private readonly JwtSettings _jwtSettings;
    private readonly ApplicationDbContext _context;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _userRepoMock = new Mock<IUserRepository>();
        _tokenServiceMock = new Mock<ITokenService>();
        _loggerMock = new Mock<ILogger<AuthController>>();

        _jwtSettings = new JwtSettings
        {
            SecretKey = "TestSecretKeyForUnitTesting123456!@#",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            AccessTokenExpirationMinutes = 60,
            RefreshTokenExpirationDays = 7
        };

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _controller = new AuthController(
            _userRepoMock.Object,
            _tokenServiceMock.Object,
            Options.Create(_jwtSettings),
            _context,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task Register_WithExistingEmail_ReturnsBadRequest()
    {
        // Arrange
        _userRepoMock.Setup(x => x.EmailExistsAsync("test@test.com"))
            .ReturnsAsync(true);

        var request = new RegisterRequest
        {
            Email = "test@test.com",
            Username = "testuser",
            FirstName = "Test",
            LastName = "User",
            Password = "Password1!",
            ConfirmPassword = "Password1!"
        };

        // Act
        var result = await _controller.Register(request);

        // Assert
        var badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Register_WithExistingUsername_ReturnsBadRequest()
    {
        // Arrange
        _userRepoMock.Setup(x => x.EmailExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(false);
        _userRepoMock.Setup(x => x.UsernameExistsAsync("existinguser"))
            .ReturnsAsync(true);

        var request = new RegisterRequest
        {
            Email = "new@test.com",
            Username = "existinguser",
            FirstName = "Test",
            LastName = "User",
            Password = "Password1!",
            ConfirmPassword = "Password1!"
        };

        // Act
        var result = await _controller.Register(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Register_WithValidData_ReturnsOkWithTokens()
    {
        // Arrange
        _userRepoMock.Setup(x => x.EmailExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(false);
        _userRepoMock.Setup(x => x.UsernameExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(false);
        _userRepoMock.Setup(x => x.CreateAsync(It.IsAny<User>()))
            .ReturnsAsync((User u) => u);
        _userRepoMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(true);
        _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<User>()))
            .Returns("test-access-token");
        _tokenServiceMock.Setup(x => x.GenerateRefreshToken())
            .Returns("test-refresh-token");

        var request = new RegisterRequest
        {
            Email = "new@test.com",
            Username = "newuser",
            FirstName = "Test",
            LastName = "User",
            Password = "Password1!",
            ConfirmPassword = "Password1!"
        };

        // Act
        var result = await _controller.Register(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<RegisterResponse>().Subject;
        response.AccessToken.Should().Be("test-access-token");
        response.User.Should().NotBeNull();
        response.User.Email.Should().Be("new@test.com");
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
    {
        // Arrange
        _userRepoMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((User?)null);
        _userRepoMock.Setup(x => x.GetByUsernameAsync(It.IsAny<string>()))
            .ReturnsAsync((User?)null);

        var request = new LoginRequest
        {
            UsernameOrEmail = "nonexistent",
            Password = "Password1!"
        };

        // Act
        var result = await _controller.Login(request);

        // Assert
        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            Username = "testuser",
            FirstName = "Test",
            LastName = "User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPassword1!"),
            IsActive = true
        };

        _userRepoMock.Setup(x => x.GetByUsernameAsync("testuser"))
            .ReturnsAsync(user);

        var request = new LoginRequest
        {
            UsernameOrEmail = "testuser",
            Password = "WrongPassword1!"
        };

        // Act
        var result = await _controller.Login(request);

        // Assert
        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Login_WithCorrectCredentials_ReturnsOkWithTokens()
    {
        // Arrange
        var password = "CorrectPassword1!";
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            Username = "testuser",
            FirstName = "Test",
            LastName = "User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            IsActive = true
        };

        _userRepoMock.Setup(x => x.GetByUsernameAsync("testuser"))
            .ReturnsAsync(user);
        _userRepoMock.Setup(x => x.GetWithLocationAsync(1))
            .ReturnsAsync(user);
        _userRepoMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(true);
        _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<User>()))
            .Returns("access-token");
        _tokenServiceMock.Setup(x => x.GenerateRefreshToken())
            .Returns("refresh-token");

        var request = new LoginRequest
        {
            UsernameOrEmail = "testuser",
            Password = password
        };

        // Act
        var result = await _controller.Login(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<LoginResponse>().Subject;
        response.AccessToken.Should().Be("access-token");
    }
}
