using VsrSalesEngineWebApi.Core.Entities;

namespace VsrSalesEngineWebApi.Core.Interfaces;

public interface IExecutionRepository
{
    Task<ExecutionRecord?> GetByIdAsync(string id);
    Task<IReadOnlyList<ExecutionRecord>> GetAllAsync(string? jobType = null, int limit = 50);
    Task<IReadOnlyList<ExecutionRecord>> GetFailuresAsync(int limit = 50);
    Task<IReadOnlyList<ExecutionRecord>> GetDeadLettersAsync(int limit = 50);
    Task AddAsync(ExecutionRecord record);
    Task UpdateAsync(ExecutionRecord record);
    Task<Dictionary<string, int>> GetSourceFailureCountsAsync();
    Task RecordSourceFailureAsync(string sourceName);
}
