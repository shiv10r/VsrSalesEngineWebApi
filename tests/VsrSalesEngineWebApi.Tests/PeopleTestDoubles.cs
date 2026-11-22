using System.Net;
using System.Text;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Core.Services.AgentRelay.People;

namespace VsrSalesEngineWebApi.Tests;

/// <summary>Serves canned pages by URL. Anything not registered is a 404. Never touches the network.</summary>
internal sealed class FakePageFetcher : IPublicPageFetcher
{
    private readonly Dictionary<string, FetchedPage> _pages = new(StringComparer.OrdinalIgnoreCase);

    public List<string> Requests { get; } = new();
    public IEnumerable<string> PageRequests => Requests.Where(r => !r.EndsWith("/robots.txt", StringComparison.OrdinalIgnoreCase));
    public int RobotsRequests => Requests.Count(r => r.EndsWith("/robots.txt", StringComparison.OrdinalIgnoreCase));

    public FakePageFetcher Html(string url, string body) => Add(url, 200, "text/html", body, null);
    public FakePageFetcher Text(string url, string body, int status = 200) => Add(url, status, "text/plain", body, null);
    public FakePageFetcher Status(string url, int status) => Add(url, status, "text/html", string.Empty, null);
    public FakePageFetcher Fail(string url, string error) => Add(url, 0, null, string.Empty, error);

    private FakePageFetcher Add(string url, int status, string? type, string body, string? error)
    {
        _pages[url] = new FetchedPage(new Uri(url), new Uri(url), status, type, body, false, error);
        return this;
    }

    public Task<FetchedPage> GetAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        Requests.Add(uri.ToString());
        return Task.FromResult(_pages.TryGetValue(uri.ToString(), out var page)
            ? page
            : new FetchedPage(uri, uri, 404, "text/html", string.Empty, false, null));
    }
}

/// <summary>Resolves host names from a table. A host can answer differently on later lookups (DNS rebinding).</summary>
internal sealed class FakeResolver : IHostResolver
{
    private readonly Dictionary<string, List<IPAddress[]>> _answers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _calls = new(StringComparer.OrdinalIgnoreCase);

    public FakeResolver Map(string host, params string[] addresses) => Then(host, addresses);

    public FakeResolver Then(string host, params string[] addresses)
    {
        if (!_answers.TryGetValue(host, out var list)) _answers[host] = list = new List<IPAddress[]>();
        list.Add(addresses.Select(IPAddress.Parse).ToArray());
        return this;
    }

    public Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken = default)
    {
        if (!_answers.TryGetValue(host, out var list)) return Task.FromResult<IReadOnlyList<IPAddress>>(Array.Empty<IPAddress>());
        var call = _calls.GetValueOrDefault(host);
        _calls[host] = call + 1;
        return Task.FromResult<IReadOnlyList<IPAddress>>(list[Math.Min(call, list.Count - 1)]);
    }
}

internal sealed record RecordedRequest(Uri Uri, string UserAgent, string Accept);

/// <summary>An HTTP handler that answers from a function and records what was asked.</summary>
internal sealed class ScriptedHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

    public ScriptedHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

    public List<RecordedRequest> Requests { get; } = new();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(new RecordedRequest(request.RequestUri!, request.Headers.UserAgent.ToString(), request.Headers.Accept.ToString()));
        return Task.FromResult(_respond(request));
    }

    public static HttpResponseMessage Page(string body, string mediaType = "text/html") =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, mediaType) };

    public static HttpResponseMessage Redirect(string location) =>
        new(HttpStatusCode.Found) { Headers = { Location = new Uri(location, UriKind.RelativeOrAbsolute) } };
}

/// <summary>A finder that always fails, to prove one bad company cannot stop a run.</summary>
internal sealed class ThrowingFinder : IRelatedPeopleFinder
{
    public Task<RelatedPeopleResult> FindAsync(Opportunity opportunity, bool allowNetwork, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("boom");
}

/// <summary>Answers with a fixed result for chosen companies and delegates the rest.</summary>
internal sealed class ScriptedFinder : IRelatedPeopleFinder
{
    private readonly Func<Opportunity, bool, RelatedPeopleResult> _answer;

    public ScriptedFinder(Func<Opportunity, bool, RelatedPeopleResult> answer) => _answer = answer;

    public List<(string Company, bool AllowNetwork)> Calls { get; } = new();

    public Task<RelatedPeopleResult> FindAsync(Opportunity opportunity, bool allowNetwork, CancellationToken cancellationToken = default)
    {
        Calls.Add((opportunity.Company, allowNetwork));
        return Task.FromResult(_answer(opportunity, allowNetwork));
    }
}
