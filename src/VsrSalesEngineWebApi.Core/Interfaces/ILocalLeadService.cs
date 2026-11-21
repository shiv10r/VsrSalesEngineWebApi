using VsrSalesEngineWebApi.Core.DTOs;

namespace VsrSalesEngineWebApi.Core.Interfaces;

public interface ILocalLeadService
{
    LocalLeadOptionsDto GetOptions();
    Task<LocalLeadPageDto> GetPagedAsync(LocalLeadFilterDto filter, CancellationToken cancellationToken = default);
    Task<LocalLeadSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);

    /// <exception cref="ArgumentException">Validation failed or the lead already exists.</exception>
    Task<LocalLeadDto> CreateAsync(CreateLocalLeadDto dto, string actor, CancellationToken cancellationToken = default);

    /// <exception cref="ArgumentException">Validation failed.</exception>
    Task<LocalLeadDto?> UpdateAsync(string id, UpdateLocalLeadDto dto, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <exception cref="ArgumentException">No rows, or more than the maximum.</exception>
    Task<ImportLocalLeadsResultDto> ImportAsync(ImportLocalLeadsRequestDto request, string actor, CancellationToken cancellationToken = default);
}
