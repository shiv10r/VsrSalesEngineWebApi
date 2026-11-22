using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Core.Services.LocalLeads;

namespace VsrSalesEngineWebApi.Places;

/// <summary>Official Google Places API (New): Text Search and Place Details. The API key only ever travels in a header.</summary>
public class GooglePlacesClient : IPlacesClient
{
    public const string HttpClientName = "GooglePlaces";
    public const string SearchUrl = "https://places.googleapis.com/v1/places:searchText";
    public const string DetailsUrl = "https://places.googleapis.com/v1/places/";

    public const string SearchFieldMask =
        "places.id,places.displayName,places.formattedAddress,places.googleMapsUri,places.websiteUri," +
        "places.nationalPhoneNumber,places.rating,places.userRatingCount,places.primaryTypeDisplayName";

    public const string DetailsFieldMask =
        "id,displayName,formattedAddress,googleMapsUri,websiteUri,nationalPhoneNumber,rating,userRatingCount,primaryTypeDisplayName";

    private readonly IHttpClientFactory _factory;
    private readonly GooglePlacesOptions _options;
    private readonly PlacesUsageLimiter _limiter;

    public GooglePlacesClient(IHttpClientFactory factory, GooglePlacesOptions options, PlacesUsageLimiter limiter)
    {
        _factory = factory;
        _options = options;
        _limiter = limiter;
    }

    public bool IsConfigured => _options.IsConfigured;

    public async Task<IReadOnlyList<PlaceInfo>> SearchTextAsync(string textQuery, int maxResultCount, CancellationToken cancellationToken = default)
    {
        var body = new JsonObject
        {
            ["textQuery"] = textQuery,
            ["languageCode"] = "en",
            ["regionCode"] = "IN",
            ["maxResultCount"] = maxResultCount
        };
        var request = new HttpRequestMessage(HttpMethod.Post, SearchUrl)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        };

        var json = await SendAsync(request, SearchFieldMask, cancellationToken);
        return json["places"] is JsonArray places
            ? places.OfType<JsonObject>().Select(Map).Where(p => p.PlaceId.Length > 0).ToList()
            : Array.Empty<PlaceInfo>();
    }

    public async Task<PlaceInfo> GetDetailsAsync(string placeId, CancellationToken cancellationToken = default)
    {
        var url = DetailsUrl + Uri.EscapeDataString(placeId) + "?languageCode=en&regionCode=IN";
        return Map(await SendAsync(new HttpRequestMessage(HttpMethod.Get, url), DetailsFieldMask, cancellationToken));
    }

    private async Task<JsonObject> SendAsync(HttpRequestMessage request, string fieldMask, CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured) throw new PlacesException(PlacesFailure.NotConfigured, "Google Maps search is not set up.");
        if (!_limiter.TryAcquire())
        {
            throw new PlacesException(PlacesFailure.DailyCapReached,
                $"Daily Google Maps search limit ({_limiter.DailyCap}) reached. It resets at midnight UTC.");
        }

        request.Headers.TryAddWithoutValidation("X-Goog-Api-Key", _options.ApiKey.Trim());
        request.Headers.TryAddWithoutValidation("X-Goog-FieldMask", fieldMask);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            using var response = await _factory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode) throw MapError(response.StatusCode);
            return JsonNode.Parse(text) as JsonObject ?? new JsonObject();
        }
        catch (PlacesException) { throw; }
        catch (JsonException)
        {
            throw new PlacesException(PlacesFailure.Unavailable, "Google Maps returned an unreadable answer. Try again shortly.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new PlacesException(PlacesFailure.Unavailable, "Could not reach Google Maps. Try again shortly.");
        }
    }

    private static PlacesException MapError(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new PlacesException(PlacesFailure.KeyRejected,
            "Google rejected the API key. Check that Places API (New) is enabled and the key restrictions allow this server."),
        HttpStatusCode.BadRequest => new PlacesException(PlacesFailure.KeyRejected,
            "Google did not accept the request. Check that the API key is valid and Places API (New) is enabled."),
        HttpStatusCode.TooManyRequests => new PlacesException(PlacesFailure.QuotaExceeded,
            "Google Maps quota is used up for now. Try again later or raise the quota in Google Cloud."),
        HttpStatusCode.NotFound => new PlacesException(PlacesFailure.NotFound, "Google no longer has this place."),
        _ => new PlacesException(PlacesFailure.Unavailable, "Google Maps is having trouble right now. Try again shortly.")
    };

    private static PlaceInfo Map(JsonObject p) => new(
        Text(p["id"]),
        Text(p["displayName"]?["text"]),
        Text(p["formattedAddress"]),
        Text(p["nationalPhoneNumber"]),
        Text(p["websiteUri"]),
        p["rating"] is JsonValue r && r.TryGetValue<double>(out var rating) ? rating : null,
        p["userRatingCount"] is JsonValue c && c.TryGetValue<int>(out var count) ? count : null,
        Text(p["googleMapsUri"]),
        Text(p["primaryTypeDisplayName"]?["text"]));

    private static string Text(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue<string>(out var s) ? s.Trim() : string.Empty;
}
