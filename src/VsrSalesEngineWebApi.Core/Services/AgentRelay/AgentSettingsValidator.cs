using System.Globalization;
using System.Text.RegularExpressions;
using VsrSalesEngineWebApi.Core.DTOs;

namespace VsrSalesEngineWebApi.Core.Services.AgentRelay;

/// <summary>Checks an agent-settings update. Field names look like 'agents[2].name' so a form can mark the exact input.</summary>
public static class AgentSettingsValidator
{
    private static readonly Regex NamePattern = new(@"^[\p{L}][\p{L}' \-]*$", RegexOptions.Compiled);

    public static List<ValidationErrorDto> Validate(
        UpdateAgentSettingsDto dto,
        IReadOnlyCollection<string> scheduleIds,
        IReadOnlyCollection<string> availableSources)
    {
        var errors = new List<ValidationErrorDto>();
        ValidateAgents(dto.Agents, errors);

        if (dto.DraftThreshold is < 0 or > 100)
        {
            errors.Add(new("draftThreshold", "The draft threshold must be between 0 and 100."));
        }

        ValidateKeywords(dto.Keywords, errors);
        ValidateShifts(dto.Shifts, scheduleIds, availableSources, errors);
        return errors;
    }

    private static void ValidateAgents(List<UpdateAgentDto>? agents, List<ValidationErrorDto> errors)
    {
        if (agents == null || agents.Count == 0)
        {
            errors.Add(new("agents", "Send all 8 agents."));
            return;
        }

        var seenIds = new HashSet<string>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < agents.Count; i++)
        {
            var agent = agents[i];
            var field = $"agents[{i}]";
            var info = AgentCatalog.All.FirstOrDefault(a => a.Id == agent.Id);

            if (info == null)
            {
                errors.Add(new($"{field}.id", $"Unknown agent '{agent.Id}'."));
                continue;
            }

            if (!seenIds.Add(agent.Id))
            {
                errors.Add(new($"{field}.id", $"Agent '{agent.Id}' appears twice."));
                continue;
            }

            var nameError = CheckName(agent.Name, seenNames);
            if (nameError != null) errors.Add(new($"{field}.name", nameError));

            if (!agent.Enabled && AgentCatalog.IsRequired(agent.Id))
            {
                errors.Add(new($"{field}.enabled",
                    $"The {AgentCatalog.JobLabel(agent.Id)} must stay on: the relay cannot run without it."));
            }
        }

        foreach (var info in AgentCatalog.All.Where(a => !seenIds.Contains(a.Id)))
        {
            errors.Add(new("agents", $"Agent '{info.Id}' is missing. Send all 8 agents."));
        }

        var drafter = agents.FirstOrDefault(a => a.Id == AgentIds.Drafter);
        var complianceIndex = agents.FindIndex(a => a.Id == AgentIds.Compliance);
        if (drafter is { Enabled: true } && complianceIndex >= 0 && !agents[complianceIndex].Enabled)
        {
            errors.Add(new($"agents[{complianceIndex}].enabled",
                "The Compliance agent must stay on while the Drafter is on, because every draft needs a compliance check."));
        }
    }

    private static string? CheckName(string? raw, HashSet<string> seenNames)
    {
        var name = (raw ?? string.Empty).Trim();
        if (name.Length == 0) return "Give this agent a name.";
        if (name.Length > 24) return "A name can be at most 24 characters.";
        if (!NamePattern.IsMatch(name)) return "Use letters, spaces, apostrophes and hyphens only.";
        return seenNames.Add(name) ? null : "Another agent already has this name.";
    }

    private static void ValidateKeywords(UpdateKeywordsDto? keywords, List<ValidationErrorDto> errors)
    {
        if (keywords == null)
        {
            errors.Add(new("keywords", "Send the target and excluded keywords."));
            return;
        }

        CheckKeywordList("keywords.target", keywords.Target, requireOne: true, errors);
        CheckKeywordList("keywords.excluded", keywords.Excluded, requireOne: false, errors);
    }

    private static void CheckKeywordList(string field, List<string>? list, bool requireOne, List<ValidationErrorDto> errors)
    {
        var items = list ?? new List<string>();
        if (requireOne && items.All(string.IsNullOrWhiteSpace))
        {
            errors.Add(new(field, "Add at least one target keyword."));
            return;
        }

        if (items.Count > 30) errors.Add(new(field, "Use at most 30 keywords."));

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i]?.Trim() ?? string.Empty;
            if (item.Length > 50) errors.Add(new($"{field}[{i}]", "A keyword can be at most 50 characters."));
            if (item.Contains(',')) errors.Add(new($"{field}[{i}]", "A keyword cannot contain a comma."));
        }
    }

    private static void ValidateShifts(
        List<UpdateShiftDto>? shifts,
        IReadOnlyCollection<string> scheduleIds,
        IReadOnlyCollection<string> availableSources,
        List<ValidationErrorDto> errors)
    {
        if (shifts == null)
        {
            errors.Add(new("shifts", "Send the shifts."));
            return;
        }

        var seen = new HashSet<string>();
        for (var i = 0; i < shifts.Count; i++)
        {
            var shift = shifts[i];
            var field = $"shifts[{i}]";

            if (!scheduleIds.Contains(shift.ScheduleId)) errors.Add(new($"{field}.scheduleId", $"Unknown shift '{shift.ScheduleId}'."));
            else if (!seen.Add(shift.ScheduleId)) errors.Add(new($"{field}.scheduleId", "This shift appears twice."));

            if (shift.IntervalHours is < 1 or > 24) errors.Add(new($"{field}.intervalHours", "Run every 1 to 24 hours."));

            var sources = shift.Sources ?? new List<string>();
            if (shift.Enabled && sources.Count == 0) errors.Add(new($"{field}.sources", "Pick at least one source for a shift that is on."));
            for (var j = 0; j < sources.Count; j++)
            {
                if (!availableSources.Contains(sources[j], StringComparer.OrdinalIgnoreCase))
                {
                    errors.Add(new($"{field}.sources[{j}]", $"'{sources[j]}' is not an available source."));
                }
            }

            if (!TryTime(shift.ActiveFrom, out _)) errors.Add(new($"{field}.activeFrom", "Use a time like 06:00."));
            if (!TryTime(shift.ActiveTo, out _)) errors.Add(new($"{field}.activeTo", "Use a time like 14:00."));
        }
    }

    public static bool TryTime(string? value, out TimeSpan time) =>
        TimeSpan.TryParseExact(value?.Trim(), "hh\\:mm", CultureInfo.InvariantCulture, out time);
}
