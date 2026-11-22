using System.Net;

namespace VsrSalesEngineWebApi.Core.Interfaces;

/// <summary>One page read from a public website. StatusCode is 0 when nothing was fetched (blocked, timeout, error).</summary>
public record FetchedPage(Uri RequestedUri, Uri FinalUri, int StatusCode, string? ContentType, string Body, bool Truncated, string? Error)
{
    public bool IsHtml => StatusCode == 200 && Error == null
        && ContentType != null && ContentType.Contains("html", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Plain GET of a public page: no JavaScript, no login, bounded size and time, never a private address.</summary>
public interface IPublicPageFetcher
{
    Task<FetchedPage> GetAsync(Uri uri, CancellationToken cancellationToken = default);
}

public interface IHostResolver
{
    Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken = default);
}
