using VsrSalesEngineWebApi.Core.DTOs;

namespace VsrSalesEngineWebApi.Core.Interfaces;

/// <summary>When leads arrive: new leads and agent runs bucketed by day of week and hour (India time).</summary>
public interface ILeadHeatmapService
{
    Task<LeadHeatmapDto> GetHeatmapAsync(int days, CancellationToken cancellationToken = default);
}
