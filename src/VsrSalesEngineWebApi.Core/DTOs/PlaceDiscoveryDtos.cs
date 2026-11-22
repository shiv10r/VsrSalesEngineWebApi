namespace VsrSalesEngineWebApi.Core.DTOs;

public record DiscoverPlacesRequestDto(string? District, string? Category, string? Query, int? MaxResults);

public record PlaceResultDto(
    string PlaceId,
    string Name,
    string Address,
    string Phone,
    string Website,
    bool HasWebsite,
    double? Rating,
    int? ReviewCount,
    string MapsUrl,
    string SuggestedCategory,
    string District);

public record DiscoverPlacesResponseDto(bool Configured, IReadOnlyList<PlaceResultDto> Results, int RemainingToday);

public record PlacesStatusDto(bool Configured, int DailyCap, int RemainingToday);

public record SavePlaceRequestDto(string? PlaceId, string? Name, string? District, string? Category, string? MapsUrl);

public record SavePlaceResultDto(LocalLeadDto Lead, bool AlreadySaved);

/// <summary>Fetched live from Google for display only. Never stored.</summary>
public record PlaceLiveDetailsDto(
    string PlaceId,
    string Address,
    string Phone,
    string Website,
    bool HasWebsite,
    double? Rating,
    int? ReviewCount,
    string MapsUrl);
