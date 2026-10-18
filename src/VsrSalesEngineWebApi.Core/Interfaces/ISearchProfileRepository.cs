using VsrSalesEngineWebApi.Core.Entities;

namespace VsrSalesEngineWebApi.Core.Interfaces;

public interface ISearchProfileRepository
{
    Task<IReadOnlyList<SearchProfile>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<SearchProfile?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<SearchProfile> CreateAsync(SearchProfile profile, CancellationToken cancellationToken = default);
    Task<SearchProfile?> UpdateAsync(SearchProfile profile, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
}
