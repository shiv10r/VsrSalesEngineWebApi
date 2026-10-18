using VsrSalesEngineWebApi.Core.DTOs;

namespace VsrSalesEngineWebApi.Core.Interfaces;

public interface IOpportunityService
{
    Task<IReadOnlyList<OpportunityDto>> GetOpportunitiesAsync(OpportunityFilterDto? filter = null, CancellationToken cancellationToken = default);
    Task<OpportunityDto?> GetOpportunityByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<OpportunityDto> CreateOpportunityAsync(CreateOpportunityDto dto, CancellationToken cancellationToken = default);
    Task<OpportunityDto?> UpdateStatusAsync(string id, string newStatus, CancellationToken cancellationToken = default);
}

