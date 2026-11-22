using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Core.Services.LocalLeads;

/// <summary>
/// Live Google Maps search through the official Places API. Search results are never stored; saving a result
/// keeps only name, district, category, placeId and mapsUrl (Google's terms restrict storing other Maps content).
/// </summary>
public class PlaceDiscoveryService : IPlaceDiscoveryService
{
    public const string SourceName = "Google Maps";
    public const int DefaultMaxResults = 10;
    public const int MaxResultsLimit = 20;
    private const int MaxQueryLength = 80;

    private static readonly Dictionary<string, string> CategoryKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Healthcare"] = "clinics and hospitals",
        ["Education & Coaching"] = "schools and coaching centres",
        ["Restaurants & Cafes"] = "restaurants and cafes",
        ["Retail & Shops"] = "shops and stores",
        ["Real Estate"] = "real estate agents",
        ["Hotels & Travel"] = "hotels and travel agencies",
        ["Manufacturing & MSME"] = "manufacturers and factories",
        ["Professional Services"] = "consultants and professional services",
        ["Construction & Trades"] = "contractors and construction",
        ["Other"] = "local businesses"
    };

    private readonly IPlacesClient _places;
    private readonly PlacesUsageLimiter _limiter;
    private readonly ILocalLeadRepository _repository;

    public PlaceDiscoveryService(IPlacesClient places, PlacesUsageLimiter limiter, ILocalLeadRepository repository)
    {
        _places = places;
        _limiter = limiter;
        _repository = repository;
    }

    public PlacesStatusDto GetStatus() => new(_places.IsConfigured, _limiter.DailyCap, _limiter.Remaining);

    public async Task<DiscoverPlacesResponseDto> SearchAsync(DiscoverPlacesRequestDto request, CancellationToken cancellationToken = default)
    {
        var district = UttarPradesh.Resolve(request.District)
            ?? throw new ArgumentException("district must be one of the Uttar Pradesh districts");

        string? category = null;
        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            category = LocalLeadCategoriser.MatchKnown(request.Category)
                ?? throw new ArgumentException($"category must be one of: {string.Join(", ", LocalLeadCategoriser.Categories)}.");
        }

        var query = (request.Query ?? string.Empty).Trim();
        if (query.Length > MaxQueryLength) throw new ArgumentException($"query is too long (max {MaxQueryLength} characters)");
        var max = Math.Clamp(request.MaxResults ?? DefaultMaxResults, 1, MaxResultsLimit);

        var keywords = query.Length > 0 ? query : category is null ? "businesses" : CategoryKeywords[category];
        var found = await _places.SearchTextAsync($"{keywords} in {district}, Uttar Pradesh, India", max, cancellationToken);

        var results = found.Select(p => ToResult(p, district, category)).ToList();
        return new DiscoverPlacesResponseDto(true, results, _limiter.Remaining);
    }

    public async Task<SavePlaceResultDto> SaveAsync(SavePlaceRequestDto request, string actor, CancellationToken cancellationToken = default)
    {
        var placeId = (request.PlaceId ?? string.Empty).Trim();
        if (placeId.Length == 0 || placeId.Length > 300) throw new ArgumentException("placeId is required");

        var mapsUrl = (request.MapsUrl ?? string.Empty).Trim();
        if (mapsUrl.Length > 0 && (mapsUrl.Length > 500 || !Uri.TryCreate(mapsUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("mapsUrl must be an https link");
        }

        var existing = await _repository.GetByPlaceIdAsync(placeId, cancellationToken);
        if (existing is not null) return new SavePlaceResultDto(LocalLeadService.ToDto(existing), true);

        // Only these fields are kept. Address, phone, website and rating stay live in Google.
        var input = new LeadInput(request.Name, request.District, request.Category, null, null, null, null, null, null);
        var (lead, error) = LocalLeadBuilder.Build(input, SourceName, SourceName, actor);
        if (lead is null) throw new ArgumentException(error);

        lead.PlaceId = placeId;
        lead.MapsUrl = mapsUrl.Length > 0 ? mapsUrl : null;
        return new SavePlaceResultDto(LocalLeadService.ToDto(await _repository.AddAsync(lead, cancellationToken)), false);
    }

    public async Task<PlaceLiveDetailsDto> GetLiveDetailsAsync(string leadId, CancellationToken cancellationToken = default)
    {
        var lead = await _repository.GetByIdAsync(leadId, cancellationToken)
            ?? throw new PlacesException(PlacesFailure.NotFound, $"Local lead '{leadId}' not found.");
        if (string.IsNullOrWhiteSpace(lead.PlaceId))
        {
            throw new PlacesException(PlacesFailure.NotFound, "This lead is not linked to a Google Maps place.");
        }

        var p = await _places.GetDetailsAsync(lead.PlaceId, cancellationToken);
        return new PlaceLiveDetailsDto(p.PlaceId, p.Address, p.Phone, p.Website, p.Website.Length > 0, p.Rating, p.ReviewCount,
            p.MapsUrl.Length > 0 ? p.MapsUrl : lead.MapsUrl ?? string.Empty);
    }

    private static PlaceResultDto ToResult(PlaceInfo p, string district, string? requestedCategory)
    {
        var suggested = LocalLeadCategoriser.Categorise(p.Name, p.PrimaryType).Category;
        if (suggested == LocalLeadCategoriser.Other && requestedCategory is not null) suggested = requestedCategory;

        return new PlaceResultDto(p.PlaceId, p.Name, p.Address, p.Phone, p.Website, p.Website.Length > 0,
            p.Rating, p.ReviewCount, p.MapsUrl, suggested, district);
    }
}
