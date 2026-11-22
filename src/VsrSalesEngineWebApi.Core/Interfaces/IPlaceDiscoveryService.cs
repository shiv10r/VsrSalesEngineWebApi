using VsrSalesEngineWebApi.Core.DTOs;

namespace VsrSalesEngineWebApi.Core.Interfaces;

public interface IPlaceDiscoveryService
{
    PlacesStatusDto GetStatus();

    /// <exception cref="ArgumentException">District or category invalid.</exception>
    /// <exception cref="PlacesException">Feature off, cap reached or Google error.</exception>
    Task<DiscoverPlacesResponseDto> SearchAsync(DiscoverPlacesRequestDto request, CancellationToken cancellationToken = default);

    /// <exception cref="ArgumentException">Validation failed.</exception>
    Task<SavePlaceResultDto> SaveAsync(SavePlaceRequestDto request, string actor, CancellationToken cancellationToken = default);

    /// <exception cref="PlacesException">NotFound (no lead or no place), not configured, cap or Google error.</exception>
    Task<PlaceLiveDetailsDto> GetLiveDetailsAsync(string leadId, CancellationToken cancellationToken = default);
}
