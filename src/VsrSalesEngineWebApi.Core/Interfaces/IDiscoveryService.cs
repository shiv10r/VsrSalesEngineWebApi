using VsrSalesEngineWebApi.Core.DTOs;

namespace VsrSalesEngineWebApi.Core.Interfaces;

public interface IDiscoveryService
{
    Task<IReadOnlyList<SourceDefinitionDto>> GetSourcesAsync(CancellationToken cancellationToken = default);
    Task<SourceDefinitionDto?> UpdateSourceStatusAsync(string id, UpdateSourceStatusDto dto, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SearchProfileDto>> GetProfilesAsync(CancellationToken cancellationToken = default);
    Task<SearchProfileDto?> GetProfileByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<SearchProfileDto> CreateProfileAsync(CreateSearchProfileDto dto, string actor, CancellationToken cancellationToken = default);
    Task<SearchProfileDto?> UpdateProfileAsync(string id, UpdateSearchProfileDto dto, string actor, CancellationToken cancellationToken = default);
    Task<bool> DeleteProfileAsync(string id, string actor, CancellationToken cancellationToken = default);

    Task<DiscoveryRunResultDto> RunDiscoveryAsync(RunDiscoveryRequestDto request, string actor, CancellationToken cancellationToken = default);
}
