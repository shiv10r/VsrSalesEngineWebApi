using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Entities;

namespace VsrSalesEngineWebApi.Core.Services.Reliability;

/// <summary>Turns raw execution, failure and schedule data into the reliability metrics DTO.</summary>
public static class ReliabilityMetricsBuilder
{
    public static ReliabilityMetricsDto Build(
        Dictionary<string, int> sourceFailures,
        IReadOnlyList<ExecutionRecord> allExecutions,
        IReadOnlyList<ExecutionRecord> deadLetters,
        IReadOnlyList<AutomationSchedule> schedules)
    {
        var now = DateTime.UtcNow;
        var maxLagSeconds = 0;

        foreach (var s in schedules.Where(x => x.IsEnabled))
        {
            if (s.NextRunAtUtc < now)
            {
                var lag = (int)(now - s.NextRunAtUtc).TotalSeconds;
                if (lag > maxLagSeconds) maxLagSeconds = lag;
            }
        }

        var failedSendCount = allExecutions.Count(e =>
            e.JobType.Equals("OutreachSend", StringComparison.OrdinalIgnoreCase) &&
            (e.Status.StartsWith("Failed") || e.Status == "DeadLetter"));

        var completedWithDuration = allExecutions.Where(e => e.DurationMs > 0).ToList();
        var avgDuration = completedWithDuration.Count > 0
            ? (long)completedWithDuration.Average(e => e.DurationMs)
            : 450L;

        var activeAlerts = new List<string>();
        if (maxLagSeconds > 300)
        {
            activeAlerts.Add($"Scheduler lag elevated: {maxLagSeconds}s behind expected run times");
        }

        foreach (var (src, count) in sourceFailures)
        {
            if (count >= 3)
            {
                activeAlerts.Add($"Repeated failures on connector '{src}' ({count} failures recorded)");
            }
        }

        if (deadLetters.Count > 0)
        {
            activeAlerts.Add($"Dead-letter queue contains {deadLetters.Count} unrecoverable or exhausted job(s)");
        }

        var systemHealth = activeAlerts.Count switch
        {
            0 => "Healthy",
            <= 2 => "Degraded",
            _ => "Unhealthy"
        };

        return new ReliabilityMetricsDto(
            sourceFailures,
            maxLagSeconds,
            deadLetters.Count,
            failedSendCount,
            avgDuration,
            systemHealth,
            activeAlerts
        );
    }
}
