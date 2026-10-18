using VsrSalesEngineWebApi.Core.DTOs;

namespace VsrSalesEngineWebApi.Core.Interfaces;

public interface IReliabilityService
{
    string ClassifyFailure(string errorMessage, Exception? ex = null);
    TimeSpan CalculateBackoff(int attempt);
    Task<ReliabilityMetricsDto> GetMetricsAsync();
    Task<IReadOnlyList<ExecutionRecordDto>> GetFailuresAsync();
    Task<IReadOnlyList<ExecutionRecordDto>> GetDeadLettersAsync();
    Task<RetryExecutionResultDto> RetryAsync(string executionId, CancellationToken cancellationToken = default);
    Task<ExecutionRecordDto> ExecuteWithProtectionAsync(
        string jobType,
        string referenceId,
        Func<CancellationToken, Task> action,
        int timeBudgetSeconds = 60,
        CancellationToken parentToken = default
    );
    Task<ExecutionRecordDto> SimulateFailureAsync(string failureType, string? jobType = null, string? referenceId = null);
    Task<OperationalSmokeDrillResultDto> RunSmokeDrillAsync();
}
