using VsrSalesEngineWebApi.Core.DTOs;

namespace VsrSalesEngineWebApi.Core.Interfaces;

/// <summary>
/// Runs every enabled schedule whose next-run time has passed. Shared by the in-process
/// worker and the secured run-due endpoint that an external timer calls.
/// </summary>
public interface IDueScheduleRunner
{
    Task<DueRunSummaryDto> RunDueAsync(string trigger, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AgentStatusDto>> GetStatusAsync();
}
