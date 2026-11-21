using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Entities;

namespace VsrSalesEngineWebApi.Core.Interfaces;

public interface IOpportunityRepository
{
    Task<PagedResultDto<Opportunity>> GetPagedAsync(OpportunityFilterDto filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Opportunity>> GetAllAsync(OpportunityFilterDto? filter = null, CancellationToken cancellationToken = default);
    Task<Opportunity?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<Opportunity?> GetByDeduplicationKeyAsync(string key, CancellationToken cancellationToken = default);
    Task<Opportunity> AddAsync(Opportunity opportunity, CancellationToken cancellationToken = default);
    Task<Opportunity?> UpdateAsync(Opportunity opportunity, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>When each lead was retrieved, for leads retrieved at or after <paramref name="sinceUtc"/>.</summary>
    Task<IReadOnlyList<DateTime>> GetRetrievedTimesAsync(DateTime sinceUtc, CancellationToken cancellationToken = default);
}
