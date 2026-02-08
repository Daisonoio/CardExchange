using CardExchange.API.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;

namespace CardExchange.Tests.Middleware;

public class GlobalExceptionHandlerTests
{
    private readonly Mock<ILogger<GlobalExceptionHandler>> _loggerMock;
    private readonly Mock<IHostEnvironment> _environmentMock;

    public GlobalExceptionHandlerTests()
    {
        _loggerMock = new Mock<ILogger<GlobalExceptionHandler>>();
        _environmentMock = new Mock<IHostEnvironment>();
    }

    [Fact]
    public async Task InvokeAsync_WithNoException_PassesThrough()
    {
        // Arrange
        _environmentMock.Setup(e => e.EnvironmentName).Returns("Production");
        var middleware = new GlobalExceptionHandler(
            next: (innerHttpContext) => Task.CompletedTask,
            _loggerMock.Object,
            _environmentMock.Object
        );
        var context = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task InvokeAsync_WithException_Returns500()
    {
        // Arrange
        _environmentMock.Setup(e => e.EnvironmentName).Returns("Production");
        var middleware = new GlobalExceptionHandler(
            next: (innerHttpContext) => throw new Exception("Test exception"),
            _loggerMock.Object,
            _environmentMock.Object
        );
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(500);
        context.Response.ContentType.Should().Be("application/json");
    }

    [Fact]
    public async Task InvokeAsync_WithArgumentException_Returns400()
    {
        // Arrange
        _environmentMock.Setup(e => e.EnvironmentName).Returns("Production");
        var middleware = new GlobalExceptionHandler(
            next: (innerHttpContext) => throw new ArgumentException("Invalid arg"),
            _loggerMock.Object,
            _environmentMock.Object
        );
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task InvokeAsync_WithKeyNotFoundException_Returns404()
    {
        // Arrange
        _environmentMock.Setup(e => e.EnvironmentName).Returns("Production");
        var middleware = new GlobalExceptionHandler(
            next: (innerHttpContext) => throw new KeyNotFoundException("Not found"),
            _loggerMock.Object,
            _environmentMock.Object
        );
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task InvokeAsync_InDevelopment_IncludesExceptionDetail()
    {
        // Arrange
        _environmentMock.Setup(e => e.EnvironmentName).Returns("Development");
        var middleware = new GlobalExceptionHandler(
            next: (innerHttpContext) => throw new Exception("Detailed error message"),
            _loggerMock.Object,
            _environmentMock.Object
        );
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        body.Should().Contain("Detailed error message");
    }

    [Fact]
    public async Task InvokeAsync_InProduction_HidesExceptionDetail()
    {
        // Arrange
        _environmentMock.Setup(e => e.EnvironmentName).Returns("Production");
        var middleware = new GlobalExceptionHandler(
            next: (innerHttpContext) => throw new Exception("Secret internal error"),
            _loggerMock.Object,
            _environmentMock.Object
        );
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        body.Should().NotContain("Secret internal error");
        body.Should().Contain("Errore interno del server");
    }
}
