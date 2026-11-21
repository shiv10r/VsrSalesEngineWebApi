using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;

namespace VsrSalesEngineWebApi.Security;

public static class SecurityExtensions
{
    public const string AuthRateLimitPolicy = "auth";

    private static readonly string[] KnownPlaceholderSecrets =
    {
        "VsrSalesEngineProductionSecureSecretKeyMustBe32BytesLong!",
        "VsrSalesEngineDevelopmentSecureSecretKeyMustBe32BytesLong!"
    };

    /// <summary>
    /// Refuses to start outside Development with a short or publicly known JWT signing key,
    /// because the placeholder values are in the source repository.
    /// </summary>
    public static void EnsureStrongJwtSecret(string secretKey, IHostEnvironment environment)
    {
        if (environment.IsDevelopment()) return;

        if (secretKey.Length < 32 || KnownPlaceholderSecrets.Contains(secretKey))
        {
            throw new InvalidOperationException(
                "Jwt:SecretKey is missing, too short, or still a placeholder. " +
                "Set a unique secret of at least 32 characters (environment variable Jwt__SecretKey).");
        }
    }

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            // The app runs behind the host's proxy (Render); trust it so limits apply per real client IP.
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(ClientKey(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 300,
                    Window = TimeSpan.FromMinutes(1)
                }));

            options.AddPolicy(AuthRateLimitPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(ClientKey(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1)
                }));
        });

        return services;
    }

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app, bool enableHsts)
    {
        return app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
            headers["Cache-Control"] = "no-store";
            if (enableHsts) headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";

            await next();
        });
    }

    public static IApplicationBuilder UseGenericErrorResponses(this IApplicationBuilder app)
    {
        return app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new { message = "An unexpected error occurred." });
        }));
    }

    private static string ClientKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
