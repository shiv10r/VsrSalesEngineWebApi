using System.Net;
using System.Text;
using VsrSalesEngineWebApi.Core.Services.AgentRelay.People;
using VsrSalesEngineWebApi.Infrastructure.Services.PublicWeb;
using Xunit;

namespace VsrSalesEngineWebApi.Tests;

public class PublicPageFetcherTests
{
    private static readonly HostThrottle NoThrottle = new(TimeSpan.Zero, () => DateTime.UtcNow, (_, _) => Task.CompletedTask);

    private static (PublicPageFetcher Fetcher, ScriptedHandler Handler) Build(
        Func<HttpRequestMessage, HttpResponseMessage> respond, FakeResolver? resolver = null, HostThrottle? throttle = null)
    {
        var handler = new ScriptedHandler(respond);
        var dns = resolver ?? new FakeResolver().Map("acme.io", "93.184.216.34").Map("www.acme.io", "93.184.216.35");
        return (new PublicPageFetcher(new HttpClient(handler), dns, throttle ?? NoThrottle), handler);
    }

    [Fact]
    public async Task APublicPageIsReadWithOurIdentityAndNothingElse()
    {
        var (fetcher, handler) = Build(_ => ScriptedHandler.Page("<p>hello</p>"));

        var page = await fetcher.GetAsync(new Uri("https://acme.io/team"));

        Assert.True(page.IsHtml);
        Assert.Equal("<p>hello</p>", page.Body);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("VsrSalesEngine/1.0 (+contact via repository)", request.UserAgent);
        Assert.Contains("text/html", request.Accept);
    }

    [Fact]
    public async Task AHostThatResolvesToAPrivateAddressIsNeverContacted()
    {
        var (fetcher, handler) = Build(_ => ScriptedHandler.Page("secret"), new FakeResolver().Map("internal.test", "10.1.2.3"));

        var page = await fetcher.GetAsync(new Uri("https://internal.test/"));

        Assert.NotNull(page.Error);
        Assert.Equal(0, page.StatusCode);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ARedirectToAPrivateIpAddressIsRefused()
    {
        var (fetcher, handler) = Build(_ => ScriptedHandler.Redirect("http://169.254.169.254/latest/meta-data"));

        var page = await fetcher.GetAsync(new Uri("https://acme.io/team"));

        Assert.NotNull(page.Error);
        Assert.Single(handler.Requests);                                    // the metadata address was never requested
    }

    [Fact]
    public async Task ARedirectToAnotherSiteIsNotFollowed()
    {
        var (fetcher, handler) = Build(_ => ScriptedHandler.Redirect("https://elsewhere.test/page"),
            new FakeResolver().Map("acme.io", "93.184.216.34").Map("elsewhere.test", "93.184.216.40"));

        var page = await fetcher.GetAsync(new Uri("https://acme.io/team"));

        Assert.Contains("another site", page.Error);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ASiteThatTurnsPrivateBetweenTheFirstAndSecondLookupIsStopped()
    {
        // DNS rebinding: public on the first answer, private on the next.
        var resolver = new FakeResolver().Map("acme.io", "93.184.216.34").Then("acme.io", "127.0.0.1");
        var (fetcher, handler) = Build(req => req.RequestUri!.AbsolutePath == "/team" ? ScriptedHandler.Redirect("/team/") : ScriptedHandler.Page("x"), resolver);

        var page = await fetcher.GetAsync(new Uri("https://acme.io/team"));

        Assert.Contains("blocked", page.Error);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ARelativeRedirectAndAWwwRedirectOnTheSameSiteAreFollowed()
    {
        var (fetcher, handler) = Build(req => req.RequestUri!.AbsoluteUri switch
        {
            "https://acme.io/team" => ScriptedHandler.Redirect("/people"),
            "https://acme.io/people" => ScriptedHandler.Redirect("https://www.acme.io/people"),
            _ => ScriptedHandler.Page("<p>team</p>")
        });

        var page = await fetcher.GetAsync(new Uri("https://acme.io/team"));

        Assert.True(page.IsHtml);
        Assert.Equal("https://www.acme.io/people", page.FinalUri.ToString());
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task MoreThanTwoRedirectsStop()
    {
        var (fetcher, handler) = Build(req => ScriptedHandler.Redirect(req.RequestUri!.AbsolutePath + "x"));

        var page = await fetcher.GetAsync(new Uri("https://acme.io/a"));

        Assert.Contains("too many redirects", page.Error);
        Assert.Equal(3, handler.Requests.Count);                            // the first request plus two redirects
    }

    [Fact]
    public async Task ALargePageIsCutOffAtTwoHundredKilobytes()
    {
        var (fetcher, _) = Build(_ => ScriptedHandler.Page(new string('a', 300 * 1024)));

        var page = await fetcher.GetAsync(new Uri("https://acme.io/team"));

        Assert.True(page.Truncated);
        Assert.Equal(PublicPageFetcher.MaxBodyBytes, Encoding.UTF8.GetByteCount(page.Body));
    }

    [Fact]
    public async Task NonTextContentIsNotDownloaded()
    {
        var (fetcher, _) = Build(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[50_000]) { Headers = { ContentType = new("image/png") } }
        });

        var page = await fetcher.GetAsync(new Uri("https://acme.io/logo.png"));

        Assert.Equal(string.Empty, page.Body);
        Assert.False(page.IsHtml);
    }

    [Fact]
    public async Task APageThatIsNotFoundIsAnAnswerNotAnError()
    {
        var (fetcher, _) = Build(_ => new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("nope", Encoding.UTF8, "text/html") });

        var page = await fetcher.GetAsync(new Uri("https://acme.io/team"));

        Assert.Equal(404, page.StatusCode);
        Assert.Null(page.Error);
        Assert.False(page.IsHtml);
    }

    [Fact]
    public async Task NetworkFailuresBecomeAnErrorResult()
    {
        var (fetcher, _) = Build(_ => throw new HttpRequestException("connection refused"));

        var page = await fetcher.GetAsync(new Uri("https://acme.io/team"));

        Assert.Contains("request failed", page.Error);
    }

    [Fact]
    public async Task RequestsToTheSameHostGoThroughTheOneASecondThrottle()
    {
        var waits = new List<TimeSpan>();
        var now = new DateTime(2026, 11, 22, 9, 0, 0, DateTimeKind.Utc);
        var throttle = new HostThrottle(TimeSpan.FromSeconds(1), () => now, (d, _) => { waits.Add(d); return Task.CompletedTask; });
        var (fetcher, _) = Build(_ => ScriptedHandler.Page("ok"), throttle: throttle);

        await fetcher.GetAsync(new Uri("https://acme.io/robots.txt"));
        await fetcher.GetAsync(new Uri("https://acme.io/team"));

        Assert.Equal(new[] { TimeSpan.FromSeconds(1) }, waits);
    }
}
