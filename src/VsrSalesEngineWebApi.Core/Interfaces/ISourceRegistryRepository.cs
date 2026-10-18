using VsrSalesEngineWebApi.Core.Entities;

namespace VsrSalesEngineWebApi.Core.Interfaces;

public interface ISourceRegistryRepository
{
    Task<IReadOnlyList<SourceDefinition>> GetAllSourcesAsync(CancellationToken cancellationToken = default);
    Task<SourceDefinition?> GetSourceByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<SourceDefinition?> UpdatePolicyStatusAsync(string id, string status, CancellationToken cancellationToken = default);
    Task IncrementDailyRequestCountAsync(string id, int count = 1, CancellationToken cancellationToken = default);
}
