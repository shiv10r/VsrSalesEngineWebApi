using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Entities;

namespace VsrSalesEngineWebApi.Core.Interfaces;

public interface ILocalLeadRepository
{
    Task<(IReadOnlyList<LocalLead> Items, int Total)> QueryAsync(LocalLeadFilterDto filter, CancellationToken cancellationToken = default);
    Task<LocalLead?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<LocalLead> AddAsync(LocalLead lead, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IReadOnlyCollection<LocalLead> leads, CancellationToken cancellationToken = default);
    Task<LocalLead> UpdateAsync(LocalLead lead, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>True when another lead has the same normalised name in the same district, or the same phone.</summary>
    Task<bool> DuplicateExistsAsync(string normalizedName, string district, string normalizedPhone, string? excludeId, CancellationToken cancellationToken = default);

    /// <summary>Existing "name|district" keys and phones, for fast duplicate checks during an import.</summary>
    Task<(HashSet<string> NameKeys, HashSet<string> Phones)> GetDuplicateKeysAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LocalLeadSummaryRow>> GetSummaryRowsAsync(CancellationToken cancellationToken = default);
}
