using VsrSalesEngineWebApi.Core.Entities;

namespace VsrSalesEngineWebApi.Infrastructure.Data;

/// <summary>The three shift schedules. All are switched off until the owner turns them on in Agent settings.</summary>
internal static class ShiftScheduleSeed
{
    private const string Hn = "Hacker News (Freelance & Who's Hiring)";
    private const string Wwr = "We Work Remotely (Contracts)";
    private const string Remotive = "Remotive (Contract / Freelance)";
    private const string Jobicy = "Jobicy (Developer Contracts)";

    public static IReadOnlyList<AutomationSchedule> All() => new[]
    {
        Shift("SCHED-001", "Morning shift agent (06:00-14:00 IST)", "Live Hacker News and Remotive contract leads every 2 hours", "14:00", "06:00", Hn, Remotive),
        Shift("SCHED-003", "Day shift agent (14:00-22:00 IST)", "Live We Work Remotely and Jobicy contract leads every 2 hours", "22:00", "14:00", Wwr, Jobicy),
        Shift("SCHED-004", "Night shift agent (22:00-06:00 IST)", "All four live contract feeds every 2 hours overnight", "06:00", "22:00", Hn, Wwr, Remotive, Jobicy)
    };

    private static AutomationSchedule Shift(string id, string name, string description, string quietStart, string quietEnd, params string[] sources) => new()
    {
        Id = id,
        Name = name,
        Description = description,
        Frequency = "Interval",
        IntervalHours = 2,
        CronExpression = "0 */2 * * *",
        Timezone = "Asia/Kolkata",
        IsEnabled = false,
        ProfileId = "PRF-001",
        SelectedSources = sources.ToList(),
        RunCap = 50,
        QuietHoursStart = quietStart,
        QuietHoursEnd = quietEnd,
        NextRunAtUtc = DateTime.UtcNow.AddHours(2),
        LastRunStatus = "Ready",
        CreatedBy = "System",
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow
    };
}
