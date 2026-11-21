using VsrSalesEngineWebApi.Core.Entities;

namespace VsrSalesEngineWebApi.Core.Services;

/// <summary>Pure time rules for schedules: shift windows in the schedule's own timezone and next-run calculation.</summary>
public static class ScheduleTiming
{
    /// <summary>True when "now" in the schedule's timezone falls inside its quiet-hours window (the window may cross midnight).</summary>
    public static bool IsWithinQuietHours(AutomationSchedule schedule)
    {
        if (!TimeSpan.TryParse(schedule.QuietHoursStart, out var start) ||
            !TimeSpan.TryParse(schedule.QuietHoursEnd, out var end))
        {
            return false;
        }

        var nowTod = LocalTimeOfDay(schedule.Timezone);

        return start <= end
            ? nowTod >= start && nowTod < end
            : nowTod >= start || nowTod < end;
    }

    public static TimeSpan LocalTimeOfDay(string timezone)
    {
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(timezone);
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone).TimeOfDay;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return DateTime.UtcNow.TimeOfDay;
        }
    }

    public static DateTime CalculateNextRun(string frequency, int intervalHours = 0)
    {
        var now = DateTime.UtcNow;
        if (intervalHours > 0) return now.AddHours(intervalHours);
        return frequency.ToUpperInvariant() switch
        {
            "WEEKLY" => now.AddDays(7),
            _ => now.AddHours(24) // Daily
        };
    }
}
