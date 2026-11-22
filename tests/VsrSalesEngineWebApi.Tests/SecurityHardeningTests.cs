using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using VsrSalesEngineWebApi.Security;
using Xunit;

namespace VsrSalesEngineWebApi.Tests;

public class SecurityHardeningTests
{
    private const string Placeholder = "VsrSalesEngineProductionSecureSecretKeyMustBe32BytesLong!";

    private static IHostEnvironment Env(string name) =>
        WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = name }).Environment;

    [Theory]
    [InlineData(Placeholder)]
    [InlineData("too-short")]
    [InlineData("")]
    public void Production_RejectsPlaceholderOrShortJwtSecret(string secret)
    {
        Assert.Throws<InvalidOperationException>(() =>
            SecurityExtensions.EnsureStrongJwtSecret(secret, Env("Production")));
    }

    [Fact]
    public void Production_AcceptsUniqueLongJwtSecret()
    {
        SecurityExtensions.EnsureStrongJwtSecret("a-unique-generated-secret-value-0123456789", Env("Production"));
    }

    [Fact]
    public void Development_AllowsPlaceholderJwtSecret()
    {
        SecurityExtensions.EnsureStrongJwtSecret(Placeholder, Env("Development"));
    }

    private static AuthorizationFilterContext ContextFor(string? isGuest)
    {
        var http = new DefaultHttpContext();
        var claims = isGuest == null ? Array.Empty<Claim>() : new[] { new Claim("is_guest", isGuest) };
        http.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var actionContext = new ActionContext(http, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }

    [Fact]
    public void DisallowGuest_BlocksGuestTokensWith403()
    {
        var context = ContextFor("true");

        new DisallowGuestAttribute().OnAuthorization(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public void DisallowGuest_AllowsRegularUsers()
    {
        var context = ContextFor("false");

        new DisallowGuestAttribute().OnAuthorization(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void DataChangingEndpoints_AreGuardedAgainstGuests()
    {
        var unguarded = typeof(Program).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t))
            .SelectMany(t => t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttributes(typeof(HttpMethodAttribute), true).Any(a => a is not HttpGetAttribute))
                .Where(m => !m.IsDefined(typeof(DisallowGuestAttribute), true) && !t.IsDefined(typeof(DisallowGuestAttribute), true))
                .Select(m => $"{t.Name}.{m.Name}"))
            .OrderBy(n => n)
            .ToList();

        // Intentionally open to guests: sign-in and the timer endpoint (own secret key).
        var allowed = new[]
        {
            "AgentsController.RunDue",
            "AuthController."
        };

        var unexpected = unguarded.Where(n => !allowed.Any(a => n.StartsWith(a))).ToList();
        Assert.True(unexpected.Count == 0, "Unguarded data-changing endpoints: " + string.Join(", ", unexpected));
    }

    [Fact]
    public void OnlyTheKnownEndpointsAreReachableWithoutSignIn()
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        var open = new List<string>();
        var unprotected = new List<string>();

        foreach (var type in typeof(Program).Assembly.GetTypes().Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract))
        {
            foreach (var m in type.GetMethods(flags).Where(m => m.IsDefined(typeof(HttpMethodAttribute), true)))
            {
                var anonymous = m.IsDefined(typeof(AllowAnonymousAttribute), true) || type.IsDefined(typeof(AllowAnonymousAttribute), true);
                var authorized = m.IsDefined(typeof(AuthorizeAttribute), true) || type.IsDefined(typeof(AuthorizeAttribute), true);
                var name = $"{type.Name}.{m.Name}";
                if (anonymous) open.Add(name);
                else if (!authorized) unprotected.Add(name);
            }
        }

        Assert.True(unprotected.Count == 0, "Actions without [Authorize]: " + string.Join(", ", unprotected));
        Assert.Equal(
            new[] { "AgentsController.RunDue", "AuthController.CreateGuestSession", "AuthController.GetGuestStatus", "AuthController.Login", "HealthController.Check" },
            open.OrderBy(n => n, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void FallbackPolicy_RequiresAnAuthenticatedUser()
    {
        var options = new AuthorizationOptions();
        options.RequireAuthenticatedByDefault();

        Assert.NotNull(options.FallbackPolicy);
        Assert.Contains(options.FallbackPolicy!.Requirements, r => r is DenyAnonymousAuthorizationRequirement);
    }
}
