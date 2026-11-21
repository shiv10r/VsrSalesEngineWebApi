using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Core.Services.AgentRelay;

public class LeadHeatmapService : ILeadHeatmapService
{
    public const int MinDays = 7;
    public const int MaxDays = 90;

    private readonly IOpportunityRepository _opportunities;
    private readonly IAgentRunRepository _runs;

    public LeadHeatmapService(IOpportunityRepository opportunities, IAgentRunRepository runs)
    {
        _opportunities = opportunities;
        _runs = runs;
    }

    public async Task<LeadHeatmapDto> GetHeatmapAsync(int days, CancellationToken cancellationToken = default)
    {
        days = Math.Clamp(days, MinDays, MaxDays);
        var since = DateTime.UtcNow.AddDays(-days);

        var leadTimes = await _opportunities.GetRetrievedTimesAsync(since, cancellationToken);
        var runTimes = await _runs.GetRunStartTimesAsync(since);

        return HeatmapBucketer.Build(leadTimes, runTimes, days, HeatmapBucketer.ResolveZone());
    }
}
