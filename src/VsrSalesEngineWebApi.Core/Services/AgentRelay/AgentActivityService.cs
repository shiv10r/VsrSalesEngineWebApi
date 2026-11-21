using System.Text.Json;
using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Core.Services.AgentRelay;

/// <summary>Read side for the dashboard: agent cards, run history and the activity log.</summary>
public class AgentActivityService : IAgentActivityService
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(30);

    private readonly IAgentRunRepository _runs;
    private readonly IAutomationScheduleRepository _schedules;
    private readonly IAgentSettingsService? _settings;

    public AgentActivityService(IAgentRunRepository runs, IAutomationScheduleRepository schedules, IAgentSettingsService? settings = null)
    {
        _runs = runs;
        _schedules = schedules;
        _settings = settings;
    }

    public async Task<AgentOverviewDto> GetOverviewAsync()
    {
        var running = await _runs.GetRunningAsync(DateTime.UtcNow - StaleAfter);
        var lastRun = (await _runs.GetRunsAsync(1)).FirstOrDefault();
        var totals = (await _runs.GetTotalsAsync()).ToDictionary(t => t.AgentId);

        AgentEvent? latest = running == null ? null : await _runs.GetLatestEventAsync(running.Id);
        var currentIndex = latest == null ? -1 : AgentCatalog.IndexOf(latest.AgentId);
        var lastRunFailed = lastRun is { Status: "failed" };
        var runtime = _settings != null ? await _settings.GetRuntimeAsync() : AgentRuntimeSettings.Defaults(50);

        var cards = AgentCatalog.All.Select((agent, index) =>
        {
            totals.TryGetValue(agent.Id, out var total);

            var status = "idle";
            string? task = null;
            if (!runtime.IsEnabled(agent.Id))
            {
                task = "Switched off in settings.";
            }
            else if (running != null)
            {
                if (index == currentIndex) { status = "working"; task = latest!.Message; }
                else if (index > currentIndex) status = "waiting";
            }
            else if (lastRunFailed && agent.Id == AgentIds.Reporter)
            {
                status = "error";
                task = lastRun!.Summary;
            }

            return new AgentCardDto(
                agent.Id, runtime.NameOf(agent.Id), agent.Role, status, task, total?.LastAtUtc,
                new AgentLifetimeDto(total?.WithLead ?? 0, total?.Events ?? 0));
        }).ToList();

        var nextRun = (await _schedules.GetAllAsync())
            .Where(s => s.IsEnabled)
            .Select(s => (DateTime?)s.NextRunAtUtc)
            .Min();

        return new AgentOverviewDto(cards, lastRun == null ? null : ToDto(lastRun), nextRun, running != null);
    }

    public async Task<IReadOnlyList<AgentRunDto>> GetRunsAsync(int take) =>
        (await _runs.GetRunsAsync(take)).Select(ToDto).ToList();

    public async Task<IReadOnlyList<AgentEventDto>> GetEventsAsync(long afterId, int take, string? runId) =>
        (await _runs.GetEventsAsync(afterId, take, runId)).Select(ToDto).ToList();

    private static AgentRunDto ToDto(AgentRun r) => new(
        r.Id, r.ScheduleId, r.ShiftName, r.Trigger, r.StartedAtUtc, r.FinishedAtUtc,
        r.Status, r.Summary, r.NewLeads, r.Duplicates, r.Rejected, r.DraftsReady);

    private static AgentEventDto ToDto(AgentEvent e) => new(
        e.Id, e.RunId, e.AtUtc, e.AgentId, e.Kind, e.Message,
        e.LeadJson == null ? null : JsonSerializer.Deserialize<AgentLeadDto>(e.LeadJson, AgentLog.Json));
}
