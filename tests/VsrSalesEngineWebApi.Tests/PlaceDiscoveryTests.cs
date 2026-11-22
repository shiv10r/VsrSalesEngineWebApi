using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using VsrSalesEngineWebApi.Controllers;
using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Core.Services.LocalLeads;
using VsrSalesEngineWebApi.Infrastructure.Data;
using VsrSalesEngineWebApi.Infrastructure.Repositories;
using VsrSalesEngineWebApi.Places;
using VsrSalesEngineWebApi.Security;
using Xunit;

namespace VsrSalesEngineWebApi.Tests;

public class PlaceDiscoveryTests
{
    private const string Key = "AIza-SECRET-KEY-123";

    private const string SearchJson = """
        { "places": [
          { "id": "ChIJ1", "displayName": { "text": "Kanha Interiors" }, "formattedAddress": "Civil Lines, Gorakhpur, Uttar Pradesh",
            "googleMapsUri": "https://maps.google.com/?cid=1", "nationalPhoneNumber": "0551 234 5678",
            "rating": 4.6, "userRatingCount": 88, "primaryTypeDisplayName": { "text": "Interior designer" } },
          { "id": "ChIJ2", "displayName": { "text": "Green Hotel" }, "formattedAddress": "Gorakhpur",
            "googleMapsUri": "https://maps.google.com/?cid=2", "websiteUri": "https://greenhotel.example" }
        ] }
        """;

    private sealed class FakeHandler : HttpMessageHandler
    {
        public HttpStatusCode Status = HttpStatusCode.OK;
        public string Body = SearchJson;
        public Exception? Throw;
        public readonly List<HttpRequestMessage> Requests = new();
        public readonly List<string> Bodies = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct));
            if (Throw is not null) throw Throw;
            return new HttpResponseMessage(Status) { Content = new StringContent(Body) };
        }
    }

    private sealed class FakeFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private readonly FakeHandler _handler = new();
    private readonly VsrDbContext _context = TestDbContextFactory.CreateSeeded();

    private (PlaceDiscoveryService Service, LocalLeadRepository Repo) Build(string apiKey = Key, int cap = 100)
    {
        var options = new GooglePlacesOptions { ApiKey = apiKey, DailyRequestCap = cap };
        var limiter = new PlacesUsageLimiter(options);
        var client = new GooglePlacesClient(new FakeFactory(_handler), options, limiter);
        var repo = new LocalLeadRepository(_context);
        return (new PlaceDiscoveryService(client, limiter, repo), repo);
    }

    private static DiscoverPlacesRequestDto Req(string? district = "Gorakhpur", string? category = null, string? query = "interior designer", int? max = null) =>
        new(district, category, query, max);

    [Fact]
    public async Task Search_SendsOfficialRequestShape()
    {
        var (service, _) = Build();

        await service.SearchAsync(Req(max: 7));

        var request = Assert.Single(_handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://places.googleapis.com/v1/places:searchText", request.RequestUri!.ToString());
        Assert.Equal(Key, request.Headers.GetValues("X-Goog-Api-Key").Single());
        Assert.Equal(GooglePlacesClient.SearchFieldMask, request.Headers.GetValues("X-Goog-FieldMask").Single());
        Assert.Contains("places.primaryTypeDisplayName", GooglePlacesClient.SearchFieldMask);

        using var body = JsonDocument.Parse(_handler.Bodies[0]);
        Assert.Equal("interior designer in Gorakhpur, Uttar Pradesh, India", body.RootElement.GetProperty("textQuery").GetString());
        Assert.Equal("en", body.RootElement.GetProperty("languageCode").GetString());
        Assert.Equal("IN", body.RootElement.GetProperty("regionCode").GetString());
        Assert.Equal(7, body.RootElement.GetProperty("maxResultCount").GetInt32());
    }

    [Fact]
    public async Task Search_MapsResultsAndNeverReturnsTheKey()
    {
        var (service, _) = Build();

        var response = await service.SearchAsync(Req());

        Assert.True(response.Configured);
        Assert.Equal(99, response.RemainingToday);
        var first = response.Results[0];
        Assert.Equal("ChIJ1", first.PlaceId);
        Assert.Equal("Kanha Interiors", first.Name);
        Assert.Equal("0551 234 5678", first.Phone);
        Assert.False(first.HasWebsite);
        Assert.Equal(4.6, first.Rating);
        Assert.Equal(88, first.ReviewCount);
        Assert.Equal("Professional Services", first.SuggestedCategory);
        Assert.Equal("Gorakhpur", first.District);
        Assert.True(response.Results[1].HasWebsite);
        Assert.DoesNotContain(Key, JsonSerializer.Serialize(response));
    }

    [Fact]
    public async Task Search_UsesCategoryKeywordsAndResolvesDistrictAliases()
    {
        var (service, _) = Build();

        await service.SearchAsync(Req(district: "Allahabad", category: "healthcare", query: null));

        using var body = JsonDocument.Parse(_handler.Bodies[0]);
        Assert.Equal("clinics and hospitals in Prayagraj, Uttar Pradesh, India", body.RootElement.GetProperty("textQuery").GetString());
    }

    [Theory]
    [InlineData("Mumbai", null)]
    [InlineData(null, null)]
    [InlineData("Lucknow", "Not a category")]
    public async Task Search_RejectsBadDistrictOrCategoryWithoutCallingGoogle(string? district, string? category)
    {
        var (service, _) = Build();

        await Assert.ThrowsAsync<ArgumentException>(() => service.SearchAsync(Req(district, category)));
        Assert.Empty(_handler.Requests);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(50, 20)]
    [InlineData(null, 10)]
    public async Task Search_ClampsMaxResults(int? requested, int expected)
    {
        var (service, _) = Build();

        await service.SearchAsync(Req(max: requested));

        using var body = JsonDocument.Parse(_handler.Bodies[0]);
        Assert.Equal(expected, body.RootElement.GetProperty("maxResultCount").GetInt32());
    }

    [Fact]
    public async Task Search_WithoutKey_IsNotConfiguredAndMakesNoRequest()
    {
        var (service, _) = Build(apiKey: "");

        Assert.False(service.GetStatus().Configured);
        var ex = await Assert.ThrowsAsync<PlacesException>(() => service.SearchAsync(Req()));
        Assert.Equal(PlacesFailure.NotConfigured, ex.Failure);
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task DailyCap_RefusesOnceReached()
    {
        var (service, _) = Build(cap: 2);

        await service.SearchAsync(Req());
        await service.SearchAsync(Req());
        var ex = await Assert.ThrowsAsync<PlacesException>(() => service.SearchAsync(Req()));

        Assert.Equal(PlacesFailure.DailyCapReached, ex.Failure);
        Assert.Contains("limit", ex.Message);
        Assert.Equal(2, _handler.Requests.Count);
        Assert.Equal(0, service.GetStatus().RemainingToday);
    }

    [Fact]
    public void DailyCap_ResetsOnANewUtcDay()
    {
        var now = new DateTime(2026, 1, 1, 23, 0, 0, DateTimeKind.Utc);
        var limiter = new PlacesUsageLimiter(new GooglePlacesOptions { DailyRequestCap = 1 }, () => now);

        Assert.True(limiter.TryAcquire());
        Assert.False(limiter.TryAcquire());
        now = now.AddHours(2);
        Assert.True(limiter.TryAcquire());
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, PlacesFailure.KeyRejected)]
    [InlineData(HttpStatusCode.TooManyRequests, PlacesFailure.QuotaExceeded)]
    [InlineData(HttpStatusCode.InternalServerError, PlacesFailure.Unavailable)]
    [InlineData(HttpStatusCode.BadGateway, PlacesFailure.Unavailable)]
    public async Task GoogleErrors_MapToSafeMessagesWithoutTheKey(HttpStatusCode status, PlacesFailure expected)
    {
        _handler.Status = status;
        _handler.Body = $$"""{ "error": { "message": "bad key {{Key}}" } }""";
        var (service, _) = Build();

        var ex = await Assert.ThrowsAsync<PlacesException>(() => service.SearchAsync(Req()));

        Assert.Equal(expected, ex.Failure);
        Assert.DoesNotContain(Key, ex.Message);
        Assert.DoesNotContain("bad key", ex.Message);
    }

    [Fact]
    public async Task NetworkFailure_IsReportedAsUnavailable()
    {
        _handler.Throw = new HttpRequestException("connect failed to places.googleapis.com");
        var (service, _) = Build();

        var ex = await Assert.ThrowsAsync<PlacesException>(() => service.SearchAsync(Req()));

        Assert.Equal(PlacesFailure.Unavailable, ex.Failure);
    }

    private static SavePlaceRequestDto Save(string placeId = "ChIJ1") =>
        new(placeId, "Kanha Interiors", "Gorakhpur", "Professional Services", "https://maps.google.com/?cid=1");

    [Fact]
    public async Task Save_StoresOnlyTheAllowedFieldsAndDedupesByPlaceId()
    {
        var (service, repo) = Build();

        var first = await service.SaveAsync(Save(), "tester");
        var second = await service.SaveAsync(Save(), "tester");

        Assert.False(first.AlreadySaved);
        Assert.True(second.AlreadySaved);
        Assert.Equal(first.Lead.Id, second.Lead.Id);

        var stored = await repo.GetByPlaceIdAsync("ChIJ1");
        Assert.NotNull(stored);
        Assert.Equal("Google Maps", stored!.Source);
        Assert.Equal("Kanha Interiors", stored.BusinessName);
        Assert.Equal("Gorakhpur", stored.District);
        Assert.Equal("Professional Services", stored.Category);
        Assert.Equal("https://maps.google.com/?cid=1", stored.MapsUrl);
        Assert.Equal("", stored.Phone);
        Assert.Equal("", stored.Website);
        Assert.Equal("", stored.Address);
        Assert.Null(stored.Rating);
        Assert.Null(stored.ReviewCount);
        Assert.Empty(_handler.Requests);
        Assert.Equal(1, (await new LocalLeadService(repo).GetSummaryAsync()).Total);
    }

    [Theory]
    [InlineData("", "Kanha", "Gorakhpur")]
    [InlineData("ChIJ9", "", "Gorakhpur")]
    [InlineData("ChIJ9", "Kanha", "Mumbai")]
    public async Task Save_ValidatesInput(string placeId, string name, string district)
    {
        var (service, _) = Build();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SaveAsync(new SavePlaceRequestDto(placeId, name, district, null, null), "tester"));
    }

    [Fact]
    public async Task Save_RejectsNonHttpsMapsUrl()
    {
        var (service, _) = Build();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SaveAsync(Save() with { MapsUrl = "javascript:alert(1)" }, "tester"));
    }

    [Fact]
    public async Task LiveDetails_FetchesFromGoogleUsingTheStoredPlaceId()
    {
        _handler.Body = """{ "id": "ChIJ1", "displayName": { "text": "Kanha" }, "formattedAddress": "Civil Lines", "nationalPhoneNumber": "0551 1", "websiteUri": "https://k.example", "rating": 4.1, "userRatingCount": 9 }""";
        var (service, _) = Build();
        var saved = await service.SaveAsync(Save(), "tester");

        var details = await service.GetLiveDetailsAsync(saved.Lead.Id);

        var request = Assert.Single(_handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.StartsWith("https://places.googleapis.com/v1/places/ChIJ1", request.RequestUri!.ToString());
        Assert.Equal(GooglePlacesClient.DetailsFieldMask, request.Headers.GetValues("X-Goog-FieldMask").Single());
        Assert.Equal("0551 1", details.Phone);
        Assert.True(details.HasWebsite);
        Assert.Equal(4.1, details.Rating);
        Assert.DoesNotContain(Key, JsonSerializer.Serialize(details));
    }

    [Fact]
    public async Task LiveDetails_NotFoundWithoutAPlaceId_AndNotConfiguredWithoutKey()
    {
        var (service, repo) = Build(apiKey: "");
        var manual = await new LocalLeadService(repo).CreateAsync(new CreateLocalLeadDto("Raj Kirana Store", "Agra"), "tester");
        var saved = await service.SaveAsync(Save(), "tester");

        var missing = await Assert.ThrowsAsync<PlacesException>(() => service.GetLiveDetailsAsync("LL-NOPE"));
        var noPlace = await Assert.ThrowsAsync<PlacesException>(() => service.GetLiveDetailsAsync(manual.Id));
        var off = await Assert.ThrowsAsync<PlacesException>(() => service.GetLiveDetailsAsync(saved.Lead.Id));

        Assert.Equal(PlacesFailure.NotFound, missing.Failure);
        Assert.Equal(PlacesFailure.NotFound, noPlace.Failure);
        Assert.Equal(PlacesFailure.NotConfigured, off.Failure);
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task Controller_MapsFailuresToStatusCodes()
    {
        var offController = Controller(Build(apiKey: "").Service);

        var off = Assert.IsType<ObjectResult>(await offController.Discover(Req(), default));
        Assert.Equal(503, off.StatusCode);
        Assert.Contains("\"configured\":false", JsonSerializer.Serialize(off.Value));
        Assert.Contains("GooglePlaces__ApiKey", JsonSerializer.Serialize(off.Value));

        var onController = Controller(Build().Service);
        _handler.Status = HttpStatusCode.TooManyRequests;
        Assert.Equal(429, Assert.IsType<ObjectResult>(await onController.Discover(Req(), default)).StatusCode);
        _handler.Status = HttpStatusCode.Forbidden;
        Assert.Equal(502, Assert.IsType<ObjectResult>(await onController.Discover(Req(), default)).StatusCode);
        Assert.IsType<BadRequestObjectResult>(await onController.Discover(Req("Mumbai"), default));
        Assert.IsType<NotFoundObjectResult>(await onController.GetLiveDetails("LL-NOPE", default));
        Assert.IsType<NotFoundObjectResult>(await offController.GetLiveDetails("LL-X", default));
    }

    private static LocalLeadsController Controller(IPlaceDiscoveryService places) =>
        new(null!, places) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

    [Theory]
    [InlineData("Discover")]
    [InlineData("FromPlace")]
    public void CostAndWriteEndpoints_BlockGuestsWith403(string action)
    {
        var method = typeof(LocalLeadsController).GetMethod(action, BindingFlags.Public | BindingFlags.Instance)!;
        Assert.NotNull(method.GetCustomAttribute<DisallowGuestAttribute>());

        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("is_guest", "true") }, "test"))
        };
        var context = new AuthorizationFilterContext(new ActionContext(http, new RouteData(), new ActionDescriptor()), new List<IFilterMetadata>());
        new DisallowGuestAttribute().OnAuthorization(context);
        Assert.Equal(403, Assert.IsType<ObjectResult>(context.Result).StatusCode);
    }

    [Fact]
    public void StatusAndLiveDetails_AreReadEndpointsOpenToGuests()
    {
        foreach (var action in new[] { "GetDiscoverStatus", "GetLiveDetails" })
        {
            var method = typeof(LocalLeadsController).GetMethod(action, BindingFlags.Public | BindingFlags.Instance)!;
            Assert.Null(method.GetCustomAttribute<DisallowGuestAttribute>());
        }
    }

    [Fact]
    public void Status_ReportsConfigurationAndRemainingBudget()
    {
        var (service, _) = Build(cap: 5);

        var status = service.GetStatus();

        Assert.True(status.Configured);
        Assert.Equal(5, status.DailyCap);
        Assert.Equal(5, status.RemainingToday);
        Assert.DoesNotContain(Key, JsonSerializer.Serialize(status));
    }
}
