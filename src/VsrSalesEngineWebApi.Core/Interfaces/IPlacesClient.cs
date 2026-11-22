namespace VsrSalesEngineWebApi.Core.Interfaces;

/// <summary>One Google place as returned live by Places API (New). Never persisted beyond placeId and mapsUrl.</summary>
public record PlaceInfo(
    string PlaceId,
    string Name,
    string Address,
    string Phone,
    string Website,
    double? Rating,
    int? ReviewCount,
    string MapsUrl,
    string PrimaryType);

public enum PlacesFailure
{
    NotConfigured,
    DailyCapReached,
    KeyRejected,
    QuotaExceeded,
    NotFound,
    Unavailable
}

/// <summary>A Google Places failure with a message that is safe to show to a user (never contains the API key).</summary>
public class PlacesException : Exception
{
    public PlacesFailure Failure { get; }

    public PlacesException(PlacesFailure failure, string message) : base(message)
    {
        Failure = failure;
    }
}

/// <summary>Official Google Places API (New). Implementations must never scrape and never expose the API key.</summary>
public interface IPlacesClient
{
    bool IsConfigured { get; }

    /// <exception cref="PlacesException">Not configured, key rejected, quota exhausted or Google unavailable.</exception>
    Task<IReadOnlyList<PlaceInfo>> SearchTextAsync(string textQuery, int maxResultCount, CancellationToken cancellationToken = default);

    /// <exception cref="PlacesException">As SearchTextAsync, or NotFound when the place no longer exists.</exception>
    Task<PlaceInfo> GetDetailsAsync(string placeId, CancellationToken cancellationToken = default);
}
