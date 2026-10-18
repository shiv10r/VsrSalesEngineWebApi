using VsrSalesEngineWebApi.Core.DTOs;

namespace VsrSalesEngineWebApi.Core.Interfaces;

public interface IOpportunityService
{
    Task<PagedResultDto<OpportunityDto>> GetPagedOpportunitiesAsync(OpportunityFilterDto filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OpportunityDto>> GetOpportunitiesAsync(OpportunityFilterDto? filter = null, CancellationToken cancellationToken = default);
    Task<OpportunityDto?> GetOpportunityByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<OpportunityDto> CreateOpportunityAsync(CreateOpportunityDto dto, string actor = "System", CancellationToken cancellationToken = default);
    Task<OpportunityDto?> UpdateOpportunityAsync(string id, UpdateOpportunityDto dto, string actor = "User", CancellationToken cancellationToken = default);
    Task<OpportunityDto?> TransitionStatusAsync(string id, TransitionStatusDto dto, string actor = "User", CancellationToken cancellationToken = default);
    Task<bool> ArchiveOpportunityAsync(string id, string actor = "User", CancellationToken cancellationToken = default);
}
