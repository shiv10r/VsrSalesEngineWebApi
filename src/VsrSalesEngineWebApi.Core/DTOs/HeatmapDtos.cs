namespace VsrSalesEngineWebApi.Core.DTOs;

/// <summary>One non-empty hour slot. DayOfWeek: 0 = Monday ... 6 = Sunday. Hour: 0-23 in the heat map's timezone.</summary>
public record HeatmapCellDto(int DayOfWeek, int Hour, int NewLeads, int Runs);

public record LeadHeatmapDto(
    int Days,
    string Timezone,
    IReadOnlyList<HeatmapCellDto> Cells,
    int TotalNewLeads,
    int TotalRuns,
    DateTime? FirstRunAtUtc);
