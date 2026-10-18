using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Entities;

namespace VsrSalesEngineWebApi.Core.Interfaces;

public interface IOpportunityRepository
{
    Task<IReadOnlyList<Opportunity>> GetAllAsync(OpportunityFilterDto? filter = null, CancellationToken cancellationToken = default);
    Task<Opportunity?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<Opportunity?> GetByDeduplicationKeyAsync(string key, CancellationToken cancellationToken = default);
    Task<Opportunity> AddAsync(Opportunity opportunity, CancellationToken cancellationToken = default);
    Task<Opportunity?> UpdateStatusAsync(string id, string newStatus, CancellationToken cancellationToken = default);
}

