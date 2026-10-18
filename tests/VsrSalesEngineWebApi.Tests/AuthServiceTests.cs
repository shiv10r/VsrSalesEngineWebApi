using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Core.Security;
using VsrSalesEngineWebApi.Core.Services;
using Xunit;

namespace VsrSalesEngineWebApi.Tests;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IHostEnvironment> _envMock = new();
    private readonly IOptions<JwtOptions> _jwtOptions = Options.Create(new JwtOptions
    {
        SecretKey = "VsrSalesEngineTestingSecretKeyMustBe32BytesLong!",
        Issuer = "VsrTest",
        Audience = "VsrTestClients",
        ExpirationMinutes = 60
    });
    private readonly IConfiguration _config = new ConfigurationBuilder().Build();

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsSuccessfulTokenAndUser()
    {
        // Arrange
        var testUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "sales@vsr.local",
            PasswordHash = "ValidSecret123!",
            FullName = "VSR Sales",
            Role = "SalesMember",
            IsGuest = false
        };

        _userRepoMock.Setup(r => r.GetByEmailAsync("sales@vsr.local", It.IsAny<CancellationToken>()))
            .ReturnsAsync(testUser);

        var service = new AuthService(_userRepoMock.Object, _jwtOptions, _envMock.Object, _config);

        // Act
        var result = await service.LoginAsync(new LoginRequestDto("sales@vsr.local", "ValidSecret123!"));

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Token);
        Assert.NotNull(result.User);
        Assert.Equal("sales@vsr.local", result.User.Email);
        Assert.False(result.User.IsGuest);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorizedError()
    {
        // Arrange
        var testUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "sales@vsr.local",
            PasswordHash = "CorrectPassword!",
            FullName = "VSR Sales",
            Role = "SalesMember"
        };

        _userRepoMock.Setup(r => r.GetByEmailAsync("sales@vsr.local", It.IsAny<CancellationToken>()))
            .ReturnsAsync(testUser);

        var service = new AuthService(_userRepoMock.Object, _jwtOptions, _envMock.Object, _config);

        // Act
        var result = await service.LoginAsync(new LoginRequestDto("sales@vsr.local", "WrongPassword!"));

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Token);
        Assert.Equal("Invalid email or credentials.", result.ErrorMessage);
    }

    [Fact]
    public async Task Login_WithNonExistentUser_ReturnsUnauthorizedError()
    {
        // Arrange
        _userRepoMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var service = new AuthService(_userRepoMock.Object, _jwtOptions, _envMock.Object, _config);

        // Act
        var result = await service.LoginAsync(new LoginRequestDto("notfound@vsr.local", "SomePassword"));

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Token);
        Assert.Equal("Invalid email or credentials.", result.ErrorMessage);
    }

    [Theory]
    [InlineData("", "password")]
    [InlineData("email@vsr.local", "")]
    [InlineData("   ", "   ")]
    public async Task Login_WithEmptyInputs_RejectsImmediately(string email, string password)
    {
        // Arrange
        var service = new AuthService(_userRepoMock.Object, _jwtOptions, _envMock.Object, _config);

        // Act
        var result = await service.LoginAsync(new LoginRequestDto(email, password));

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Email and password are required.", result.ErrorMessage);
    }
}
