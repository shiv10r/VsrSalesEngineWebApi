using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Core.Security;
using VsrSalesEngineWebApi.Core.Services;
using Xunit;

namespace VsrSalesEngineWebApi.Tests;

public class GuestModeGateTests
{
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly IOptions<JwtOptions> _jwtOptions = Options.Create(new JwtOptions
    {
        SecretKey = "VsrSalesEngineTestingSecretKeyMustBe32BytesLong!",
        Issuer = "VsrTest",
        Audience = "VsrTestClients",
        ExpirationMinutes = 60
    });

    [Fact]
    public async Task CreateGuestSession_WhenDevelopmentAndFlagTrue_PermitsGuestWithRestrictedIdentity()
    {
        // Arrange
        var envMock = new Mock<IHostEnvironment>();
        envMock.Setup(e => e.EnvironmentName).Returns(Environments.Development);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Features:AllowDevelopmentGuest"] = "true"
            })
            .Build();

        var authService = new AuthService(_userRepoMock.Object, _jwtOptions, envMock.Object, config);

        // Act
        var result = await authService.CreateGuestSessionAsync();

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Token);
        Assert.NotNull(result.User);
        Assert.True(result.User.IsGuest);
        Assert.Equal("Guest", result.User.Role);
    }

    [Fact]
    public async Task CreateGuestSession_WhenDevelopmentAndFlagFalse_FailsClosed()
    {
        // Arrange
        var envMock = new Mock<IHostEnvironment>();
        envMock.Setup(e => e.EnvironmentName).Returns(Environments.Development);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Features:AllowDevelopmentGuest"] = "false"
            })
            .Build();

        var authService = new AuthService(_userRepoMock.Object, _jwtOptions, envMock.Object, config);

        // Act
        var result = await authService.CreateGuestSessionAsync();

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Token);
        Assert.Contains("disabled in this environment or disallowed by server policy", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateGuestSession_WhenProductionEvenIfFlagTrue_StrictlyRejectsBypass()
    {
        // Arrange
        var envMock = new Mock<IHostEnvironment>();
        envMock.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Features:AllowDevelopmentGuest"] = "true"
            })
            .Build();

        var authService = new AuthService(_userRepoMock.Object, _jwtOptions, envMock.Object, config);

        // Act
        var result = await authService.CreateGuestSessionAsync();

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Token);
        Assert.Contains("Production requires valid JWT credentials", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateGuestSession_WhenProductionAndFlagFalse_StrictlyRejectsBypass()
    {
        // Arrange
        var envMock = new Mock<IHostEnvironment>();
        envMock.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Features:AllowDevelopmentGuest"] = "false"
            })
            .Build();

        var authService = new AuthService(_userRepoMock.Object, _jwtOptions, envMock.Object, config);

        // Act
        var result = await authService.CreateGuestSessionAsync();

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Token);
    }

    [Theory]
    [InlineData("Development", "true", true, true)]
    [InlineData("Development", "false", true, false)]
    [InlineData("Production", "true", false, true)]
    [InlineData("Production", "false", false, false)]
    public void GetGuestStatus_ReportsAccurateEnvironmentAndFlag(
        string environmentName,
        string flagValue,
        bool expectedAllowedEnv,
        bool expectedServerFlag)
    {
        // Arrange
        var envMock = new Mock<IHostEnvironment>();
        envMock.Setup(e => e.EnvironmentName).Returns(environmentName);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Features:AllowDevelopmentGuest"] = flagValue
            })
            .Build();

        var authService = new AuthService(_userRepoMock.Object, _jwtOptions, envMock.Object, config);

        // Act
        var status = authService.GetGuestStatus();

        // Assert
        Assert.Equal(expectedAllowedEnv, status.AllowedInCurrentEnvironment);
        Assert.Equal(expectedServerFlag, status.ServerFlagEnabled);
        Assert.Equal(environmentName, status.EnvironmentName);
    }
}
