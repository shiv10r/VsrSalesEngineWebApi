using VsrSalesEngineWebApi.Core.DTOs;

namespace VsrSalesEngineWebApi.Core.Services.AgentRelay;

/// <summary>Turns the difference between two settings snapshots into a sentence for the activity feed.</summary>
public static class AgentSettingsChangeSummary
{
    /// <summary>Returns null when nothing changed.</summary>
    public static string? Describe(AgentSettingsDto before, AgentSettingsDto after, string user)
    {
        var changes = new List<string>();

        foreach (var now in after.Agents)
        {
            var old = before.Agents.FirstOrDefault(a => a.Id == now.Id);
            if (old == null) continue;

            var job = AgentCatalog.JobLabel(now.Id);
            if (old.Name != now.Name) changes.Add($"{job} renamed {old.Name} -> {now.Name}");
            if (old.Enabled != now.Enabled) changes.Add($"{now.Name} ({job}) switched {(now.Enabled ? "on" : "off")}");
        }

        if (!before.Keywords.Target.SequenceEqual(after.Keywords.Target)) changes.Add("target keywords changed");
        if (!before.Keywords.Excluded.SequenceEqual(after.Keywords.Excluded)) changes.Add("excluded keywords changed");

        foreach (var now in after.Shifts)
        {
            var old = before.Shifts.FirstOrDefault(s => s.ScheduleId == now.ScheduleId);
            if (old == null) continue;

            var label = ShortName(now.Name);
            if (old.Enabled != now.Enabled) changes.Add($"{label} switched {(now.Enabled ? "on" : "off")}");
            if (old.IntervalHours != now.IntervalHours) changes.Add($"{label} every {old.IntervalHours}h -> {now.IntervalHours}h");
            if (!old.Sources.OrderBy(s => s).SequenceEqual(now.Sources.OrderBy(s => s))) changes.Add($"{label} sources changed");
            if (old.ActiveFrom != now.ActiveFrom || old.ActiveTo != now.ActiveTo)
            {
                changes.Add($"{label} window {old.ActiveFrom}-{old.ActiveTo} -> {now.ActiveFrom}-{now.ActiveTo}");
            }
        }

        return changes.Count == 0 ? null : $"Settings changed by {user}: {string.Join("; ", changes)}.";
    }

    /// <summary>"Night shift agent (22:00-06:00 IST)" becomes "night shift".</summary>
    private static string ShortName(string name)
    {
        var cut = name.IndexOf('(');
        var text = (cut > 0 ? name[..cut] : name).Trim();
        if (text.EndsWith(" agent", StringComparison.OrdinalIgnoreCase)) text = text[..^6];
        return text.ToLowerInvariant();
    }
}
