using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Entities;

namespace VsrSalesEngineWebApi.Core.Interfaces;

public record RelayRequest(
    string ScheduleId,
    string ShiftName,
    string? ProfileId,
    IReadOnlyList<string> Sources,
    int RunCap,
    string Trigger);

public interface IAgentRelay
{
    /// <summary>Creates the run row. Returns null when another run is already in progress.</summary>
    Task<AgentRun?> BeginRunAsync(RelayRequest request);

    /// <summary>Runs the eight agents for an already-created run, writing events as it goes.</summary>
    Task<AgentRun> ExecuteAsync(string runId, RelayRequest request, CancellationToken cancellationToken = default);

    /// <summary>Begin + Execute. Returns null when another run is already in progress.</summary>
    Task<AgentRun?> RunAsync(RelayRequest request, CancellationToken cancellationToken = default);

    /// <summary>Logs an off-shift run so the dashboard shows why nothing happened.</summary>
    Task RecordSkippedAsync(RelayRequest request, string reason);

    Task<RelayRequest> BuildManualRequestAsync(string user);
}

public interface IAgentActivityService
{
    Task<AgentOverviewDto> GetOverviewAsync();
    Task<IReadOnlyList<AgentRunDto>> GetRunsAsync(int take);
    Task<IReadOnlyList<AgentEventDto>> GetEventsAsync(long afterId, int take, string? runId);
}

/// <summary>Starts a manual run in the background and returns its id (null when a run is already in progress).</summary>
public interface IManualRunLauncher
{
    Task<string?> StartAsync(string user);
}
