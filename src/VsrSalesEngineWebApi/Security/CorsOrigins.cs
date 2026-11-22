namespace VsrSalesEngineWebApi.Security;

/// <summary>
/// Exact-origin CORS allowlist. No wildcard or suffix matching: a lookalike such as https://evilvercel.app or
/// https://x.vercel.app is rejected. Local dev origins are only allowed in the Development environment.
/// </summary>
public sealed class CorsOrigins
{
    public const string ProductionFrontend = "https://vsr-sales-engine-webapp.vercel.app";
    private static readonly string[] DevelopmentOrigins = { "http://localhost:5173", "http://127.0.0.1:5173" };

    private readonly HashSet<string> _allowed = new(StringComparer.OrdinalIgnoreCase);

    public CorsOrigins(string? configuredOrigins, bool isDevelopment)
    {
        _allowed.Add(ProductionFrontend);
        if (isDevelopment)
        {
            foreach (var origin in DevelopmentOrigins) _allowed.Add(origin);
        }

        foreach (var raw in (configuredOrigins ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var origin = raw.TrimEnd('/');
            if (Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.AbsolutePath == "/" &&
                (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
            {
                _allowed.Add(origin);
            }
        }
    }

    public IReadOnlyCollection<string> Allowed => _allowed;

    public bool IsAllowed(string? origin) => !string.IsNullOrWhiteSpace(origin) && _allowed.Contains(origin);
}
