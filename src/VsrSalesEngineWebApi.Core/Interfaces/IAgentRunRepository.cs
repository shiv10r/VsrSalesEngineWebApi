using VsrSalesEngineWebApi.Core.Entities;

namespace VsrSalesEngineWebApi.Core.Interfaces;

public record AgentEventTotal(string AgentId, int Events, int WithLead, DateTime? LastAtUtc);

public interface IAgentRunRepository
{
    Task<AgentRun> AddRunAsync(AgentRun run);
    Task UpdateRunAsync(AgentRun run);
    Task<AgentRun?> GetRunAsync(string id);
    Task<IReadOnlyList<AgentRun>> GetRunsAsync(int take);
    Task<AgentRun?> GetRunningAsync(DateTime notOlderThanUtc);
    Task<int> FailStaleRunsAsync(DateTime olderThanUtc);
    Task<IReadOnlyList<DateTime>> GetRunStartTimesAsync(DateTime sinceUtc);

    Task<AgentEvent> AddEventAsync(AgentEvent agentEvent);
    Task<IReadOnlyList<AgentEvent>> GetEventsAsync(long afterId, int take, string? runId);
    Task<AgentEvent?> GetLatestEventAsync(string runId);
    Task<IReadOnlyList<AgentEventTotal>> GetTotalsAsync();
}
