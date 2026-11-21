using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Core.Security;
using VsrSalesEngineWebApi.Core.Services;
using VsrSalesEngineWebApi.Infrastructure.Data;
using Xunit;

namespace VsrSalesEngineWebApi.Tests;

public class PasswordSecurityTests
{
    [Fact]
    public void Hash_RoundTrips_AndRejectsWrongPassword()
    {
        var stored = PasswordHasher.Hash("Correct-Horse-9!");

        Assert.True(PasswordHasher.Verify("Correct-Horse-9!", stored));
        Assert.False(PasswordHasher.Verify("Correct-Horse-8!", stored));
        Assert.False(PasswordHasher.Verify("", stored));
    }

    [Fact]
    public void Hash_IsSalted_UsesPbkdf2WithAtLeast100kIterations_AndNeverContainsThePassword()
    {
        var a = PasswordHasher.Hash("SamePassword-1!");
        var b = PasswordHasher.Hash("SamePassword-1!");

        Assert.NotEqual(a, b);
        Assert.DoesNotContain("SamePassword", a);
        var parts = a.Split('$');
        Assert.Equal("PBKDF2-SHA256", parts[0]);
        Assert.True(int.Parse(parts[1]) >= 100_000);
        Assert.True(PasswordHasher.IsHashed(a));
    }

    [Theory]
    [InlineData("Admin123!")]
    [InlineData("")]
    [InlineData("PBKDF2-SHA256$1$AAAA$BBBB")] // right shape, far too few iterations
    [InlineData("PBKDF2-SHA256$100000$not-base64$not-base64")]
    public void Verify_RejectsPlainTextAndMalformedStoredValues(string stored)
    {
        Assert.False(PasswordHasher.Verify("Admin123!", stored));
        Assert.False(PasswordHasher.IsHashed(stored));
    }

    [Fact]
    public void Verify_RejectsAnUnsaltedSha256HexDigest()
    {
        var hex = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("Sales123!"))).ToLowerInvariant();

        Assert.False(PasswordHasher.Verify("Sales123!", hex));
    }

    private static AuthService BuildAuth(User? user)
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var jwt = Options.Create(new JwtOptions
        {
            SecretKey = "VsrSalesEngineTestingSecretKeyMustBe32BytesLong!",
            Issuer = "VsrTest",
            Audience = "VsrTestClients",
            ExpirationMinutes = 60
        });
        return new AuthService(repo.Object, jwt, new Mock<IHostEnvironment>().Object, new ConfigurationBuilder().Build());
    }

    [Fact]
    public async Task Login_DoesNotAcceptAPlainTextStoredPassword()
    {
        var user = new User { Email = "legacy@vsr.local", PasswordHash = "PlainSecret-123", Role = "SalesMember" };

        var result = await BuildAuth(user).LoginAsync(new LoginRequestDto("legacy@vsr.local", "PlainSecret-123"));

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Login_AcceptsTheCorrectPasswordAgainstAHash_AndRejectsUnknownEmail()
    {
        var user = new User { Email = "ok@vsr.local", PasswordHash = PasswordHasher.Hash("Right-Secret-1!"), Role = "SalesMember" };

        Assert.True((await BuildAuth(user).LoginAsync(new LoginRequestDto("ok@vsr.local", "Right-Secret-1!"))).Success);
        Assert.False((await BuildAuth(null).LoginAsync(new LoginRequestDto("nobody@vsr.local", "Right-Secret-1!"))).Success);
    }

    private static async Task<List<User>> SeededUsers(UserSeedOptions? options)
    {
        var db = new DbContextOptionsBuilder<VsrDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        var context = new VsrDbContext(db);
        context.Database.EnsureCreated();
        await VsrDbSeeder.SeedAsync(context, options);
        return await context.Users.AsNoTracking().ToListAsync();
    }

    [Fact]
    public async Task Seeding_CreatesNoUsersByDefault()
    {
        Assert.Empty(await SeededUsers(null));
        Assert.Empty(await SeededUsers(UserSeedOptions.None));
    }

    [Fact]
    public async Task Seeding_InProductionCreatesNoUsersWithoutAdminSettings()
    {
        var options = UserSeedOptions.Resolve(isDevelopment: false, adminEmail: null, adminPassword: null);

        Assert.Empty(await SeededUsers(options));
    }

    [Fact]
    public async Task Seeding_InDevelopmentCreatesTheTwoDemoUsersWithHashedPasswords()
    {
        var users = await SeededUsers(UserSeedOptions.Resolve(true, null, null));

        Assert.Equal(2, users.Count);
        Assert.All(users, u => Assert.True(PasswordHasher.IsHashed(u.PasswordHash)));
        var sales = users.Single(u => u.Email == "sales@vsrsalesengine.com");
        Assert.True(PasswordHasher.Verify("Sales123!", sales.PasswordHash));
        Assert.DoesNotContain("Sales123!", sales.PasswordHash);
        Assert.NotEqual(users[0].PasswordHash, users[1].PasswordHash);
    }

    [Fact]
    public async Task Seeding_WithAdminSettingsCreatesOneHashedAdmin()
    {
        var options = UserSeedOptions.Resolve(false, "  Boss@Example.com ", "a-long-admin-password-1");

        var users = await SeededUsers(options);

        var admin = Assert.Single(users);
        Assert.Equal("boss@example.com", admin.Email);
        Assert.Equal("Admin", admin.Role);
        Assert.True(PasswordHasher.Verify("a-long-admin-password-1", admin.PasswordHash));
        Assert.DoesNotContain("a-long-admin-password-1", admin.PasswordHash);
    }

    [Theory]
    [InlineData("boss@example.com", null)]
    [InlineData(null, "a-long-admin-password-1")]
    [InlineData("boss@example.com", "short")]
    [InlineData("not-an-email", "a-long-admin-password-1")]
    public void Resolve_RejectsHalfSetOrWeakAdminSettings(string? email, string? password)
    {
        Assert.Throws<InvalidOperationException>(() => UserSeedOptions.Resolve(false, email, password));
    }

    [Fact]
    public void Resolve_InDevelopmentIgnoresAdminSettingsAndUsesDemo()
    {
        Assert.True(UserSeedOptions.Resolve(true, "x@y.z", "short").DemoUsers);
    }
}
