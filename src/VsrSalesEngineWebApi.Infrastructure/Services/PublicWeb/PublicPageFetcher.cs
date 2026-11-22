using System.Text;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Core.Services.AgentRelay.People;

namespace VsrSalesEngineWebApi.Infrastructure.Services.PublicWeb;

/// <summary>
/// Reads one public page with a plain GET: no JavaScript, no login, five seconds, 200 KB, at most two redirects
/// that stay on the same site, never a private address, and at most one request a second per host.
/// </summary>
public sealed class PublicPageFetcher : IPublicPageFetcher
{
    public const int MaxBodyBytes = 200 * 1024;
    public const int MaxRedirects = 2;
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly HttpClient _client;
    private readonly IHostResolver _resolver;
    private readonly HostThrottle _throttle;

    public PublicPageFetcher(HttpClient client, IHostResolver resolver, HostThrottle throttle)
    {
        _client = client;
        _resolver = resolver;
        _throttle = throttle;
    }

    public async Task<FetchedPage> GetAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        var current = uri;

        for (var hop = 0; hop <= MaxRedirects; hop++)
        {
            if (!await SsrfGuard.IsSafeAsync(current, _resolver, cancellationToken))
                return Failed(uri, current, "blocked: not a public web address");

            await _throttle.WaitAsync(current.Host, cancellationToken);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(Timeout);

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, current);
                request.Headers.UserAgent.ParseAdd(CrawlerIdentity.UserAgent);
                request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml;q=0.9,text/plain;q=0.5");

                using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                var status = (int)response.StatusCode;

                if (status is >= 300 and < 400 && response.Headers.Location is { } location)
                {
                    var next = location.IsAbsoluteUri ? location : new Uri(current, location);
                    if (!SameSite(current, next)) return Failed(uri, next, "redirect to another site not followed");
                    current = next;
                    continue;
                }

                var mediaType = response.Content.Headers.ContentType?.MediaType;
                var (body, truncated) = IsTextual(mediaType)
                    ? await ReadCappedAsync(response, timeout.Token)
                    : (string.Empty, false);
                return new FetchedPage(uri, current, status, mediaType, body, truncated, null);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return Failed(uri, current, "timeout");
            }
            catch (HttpRequestException ex)
            {
                return Failed(uri, current, "request failed: " + (ex.Message.Length > 80 ? ex.Message[..80] : ex.Message));
            }
        }

        return Failed(uri, current, "too many redirects");
    }

    /// <summary>Same host, ignoring a leading "www." (example.com to www.example.com is common).</summary>
    private static bool SameSite(Uri from, Uri to)
    {
        static string Bare(string host) => host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
        return to.IsAbsoluteUri && string.Equals(Bare(from.Host), Bare(to.Host), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTextual(string? mediaType) =>
        mediaType != null && (mediaType.Contains("html", StringComparison.OrdinalIgnoreCase)
            || mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase));

    private static async Task<(string Body, bool Truncated)> ReadCappedAsync(HttpResponseMessage response, CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var buffer = new byte[MaxBodyBytes + 1];
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total), ct);
            if (read == 0) break;
            total += read;
        }

        var truncated = total > MaxBodyBytes;
        var count = Math.Min(total, MaxBodyBytes);
        return (Decoder(response).GetString(buffer, 0, count), truncated);
    }

    private static Encoding Decoder(HttpResponseMessage response)
    {
        var charset = response.Content.Headers.ContentType?.CharSet?.Trim('"');
        if (string.IsNullOrEmpty(charset)) return Encoding.UTF8;
        try { return Encoding.GetEncoding(charset); }
        catch (ArgumentException) { return Encoding.UTF8; }
    }

    private static FetchedPage Failed(Uri requested, Uri current, string error) =>
        new(requested, current, 0, null, string.Empty, false, error);
}
