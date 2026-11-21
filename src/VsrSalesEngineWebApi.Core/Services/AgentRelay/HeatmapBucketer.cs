using VsrSalesEngineWebApi.Core.DTOs;

namespace VsrSalesEngineWebApi.Core.Services.AgentRelay;

/// <summary>Pure bucketing for the lead heat map, kept separate so it can be tested without a database.</summary>
public static class HeatmapBucketer
{
    public const string PreferredZoneId = "Asia/Kolkata";

    /// <summary>The preferred zone, or UTC when the host does not know it (same fallback as schedules).</summary>
    public static TimeZoneInfo ResolveZone(string zoneId = PreferredZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    public static string ZoneLabel(TimeZoneInfo zone) =>
        zone.Id == TimeZoneInfo.Utc.Id ? "UTC" : PreferredZoneId;

    public static LeadHeatmapDto Build(
        IEnumerable<DateTime> leadTimesUtc,
        IEnumerable<DateTime> runTimesUtc,
        int days,
        TimeZoneInfo zone)
    {
        var leads = Count(leadTimesUtc, zone);
        var runTimes = runTimesUtc.ToList();
        var runs = Count(runTimes, zone);

        var cells = leads.Keys.Union(runs.Keys)
            .OrderBy(k => k.Day).ThenBy(k => k.Hour)
            .Select(k => new HeatmapCellDto(
                k.Day,
                k.Hour,
                leads.GetValueOrDefault(k),
                runs.GetValueOrDefault(k)))
            .ToList();

        DateTime? firstRun = runTimes.Count == 0 ? null : runTimes.Min();
        return new LeadHeatmapDto(days, ZoneLabel(zone), cells, leads.Values.Sum(), runs.Values.Sum(), firstRun);
    }

    private static Dictionary<(int Day, int Hour), int> Count(IEnumerable<DateTime> timesUtc, TimeZoneInfo zone)
    {
        var counts = new Dictionary<(int Day, int Hour), int>();
        foreach (var utc in timesUtc)
        {
            var asUtc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            var local = TimeZoneInfo.ConvertTimeFromUtc(asUtc, zone);
            var key = (Day: ((int)local.DayOfWeek + 6) % 7, Hour: local.Hour); // Monday = 0
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }
        return counts;
    }
}
