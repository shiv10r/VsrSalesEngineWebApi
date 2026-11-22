using System.Text.Json;
using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Core.Services.AgentRelay;

/// <summary>Reads and saves the owner's agent settings across the settings row, the search profile and the shift schedules.</summary>
public class AgentSettingsService : IAgentSettingsService
{
    public const string SettingsEventRunId = "SETTINGS";
    private static readonly char[] KeywordSeparators = { ',', ';', '\n' };

    private sealed record StoredAgent(string Id, string Name, bool Enabled);

    private readonly IAgentSettingsRepository _settings;
    private readonly ISearchProfileRepository _profiles;
    private readonly IAutomationScheduleRepository _schedules;
    private readonly ISourceRegistryRepository _sources;
    private readonly IAgentRunRepository _runs;
    private readonly AgentRelayOptions _options;

    public AgentSettingsService(
        IAgentSettingsRepository settings,
        ISearchProfileRepository profiles,
        IAutomationScheduleRepository schedules,
        ISourceRegistryRepository sources,
        IAgentRunRepository runs,
        AgentRelayOptions options)
    {
        _settings = settings;
        _profiles = profiles;
        _schedules = schedules;
        _sources = sources;
        _runs = runs;
        _options = options;
    }

    public async Task<AgentSettingsDto> GetAsync()
    {
        var stored = await _settings.GetAsync();
        var overrides = ReadStored(stored).ToDictionary(a => a.Id);

        var agents = AgentCatalog.All.Select(info =>
        {
            overrides.TryGetValue(info.Id, out var saved);
            var required = AgentCatalog.IsRequired(info.Id);
            return new AgentSettingDto(
                info.Id, saved?.Name ?? info.Name, info.Name, info.Role,
                required || (saved?.Enabled ?? true), required);
        }).ToList();

        var shiftRows = await GetShiftSchedulesAsync();
        var profile = await ResolveProfileAsync(shiftRows);

        return new AgentSettingsDto(
            agents,
            stored?.DraftThreshold ?? _options.DraftThreshold,
            new AgentKeywordsDto(SplitKeywords(profile?.TargetKeywords), SplitKeywords(profile?.ExcludedKeywords)),
            shiftRows.Select(ToShiftDto).ToList(),
            await GetAvailableSourcesAsync(),
            stored?.UpdatedBy,
            stored?.UpdatedAtUtc);
    }

    public async Task<AgentRuntimeSettings> GetRuntimeAsync()
    {
        var stored = await _settings.GetAsync();
        var saved = ReadStored(stored);

        var names = AgentCatalog.All.ToDictionary(a => a.Id, a => a.Name);
        foreach (var agent in saved.Where(a => names.ContainsKey(a.Id) && !string.IsNullOrWhiteSpace(a.Name)))
        {
            names[agent.Id] = agent.Name;
        }

        var disabled = saved.Where(a => !a.Enabled).Select(a => a.Id).ToHashSet();
        return new AgentRuntimeSettings(names, disabled, stored?.DraftThreshold ?? _options.DraftThreshold);
    }

    public async Task<SettingsSaveResult> SaveAsync(UpdateAgentSettingsDto dto, string user)
    {
        var shiftRows = await GetShiftSchedulesAsync();
        var available = await GetAvailableSourcesAsync();

        var errors = AgentSettingsValidator.Validate(dto, shiftRows.Select(s => s.Id).ToList(), available);
        if (errors.Count > 0) return new SettingsSaveResult(null, errors);

        var before = await GetAsync();

        await SaveAgentRowAsync(dto, user);
        await SaveKeywordsAsync(dto.Keywords!, shiftRows);
        await SaveShiftsAsync(dto.Shifts!, shiftRows, available);

        var after = await GetAsync();
        var summary = AgentSettingsChangeSummary.Describe(before, after, user);
        if (summary != null)
        {
            await _runs.AddEventAsync(new AgentEvent
            {
                RunId = SettingsEventRunId,
                AtUtc = DateTime.UtcNow,
                AgentId = AgentIds.Reporter,
                Kind = EventKinds.Result,
                Message = summary
            });
        }

        return new SettingsSaveResult(after, Array.Empty<ValidationErrorDto>());
    }

    private async Task SaveAgentRowAsync(UpdateAgentSettingsDto dto, string user)
    {
        var row = await _settings.GetAsync() ?? new AgentSettings();
        row.DraftThreshold = dto.DraftThreshold;
        row.AgentsJson = JsonSerializer.Serialize(
            dto.Agents!.Select(a => new StoredAgent(a.Id, a.Name.Trim(), a.Enabled)), AgentLog.Json);
        row.UpdatedBy = user;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await _settings.SaveAsync(row);
    }

    private async Task SaveKeywordsAsync(UpdateKeywordsDto keywords, IReadOnlyList<AutomationSchedule> shiftRows)
    {
        var profile = await ResolveProfileAsync(shiftRows);
        if (profile == null) return;

        profile.TargetKeywords = Join(keywords.Target);
        profile.ExcludedKeywords = Join(keywords.Excluded);
        profile.UpdatedAtUtc = DateTime.UtcNow;
        await _profiles.UpdateAsync(profile);
    }

    private async Task SaveShiftsAsync(
        IEnumerable<UpdateShiftDto> shifts, IReadOnlyList<AutomationSchedule> rows, IReadOnlyList<string> available)
    {
        foreach (var update in shifts)
        {
            var schedule = rows.First(r => r.Id == update.ScheduleId);
            var now = DateTime.UtcNow;

            schedule.IsEnabled = update.Enabled;
            schedule.IntervalHours = update.IntervalHours;
            schedule.SelectedSources = (update.Sources ?? new List<string>())
                .Select(s => available.First(a => a.Equals(s, StringComparison.OrdinalIgnoreCase)))
                .Distinct()
                .ToList();
            schedule.QuietHoursEnd = update.ActiveFrom!.Trim();
            schedule.QuietHoursStart = update.ActiveTo!.Trim();

            var latest = now.AddHours(update.IntervalHours);
            if (schedule.NextRunAtUtc > latest) schedule.NextRunAtUtc = latest;
            schedule.UpdatedAtUtc = now;

            await _schedules.UpdateAsync(schedule);
        }
    }

    private async Task<IReadOnlyList<AutomationSchedule>> GetShiftSchedulesAsync() =>
        (await _schedules.GetAllAsync())
            .Where(s => s.Frequency.Equals("Interval", StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Id)
            .ToList();

    private async Task<IReadOnlyList<string>> GetAvailableSourcesAsync() =>
        (await _sources.GetAllSourcesAsync())
            .Where(s => LiveSourceNames.IsLive(s.Name))
            .Select(s => s.Name)
            .ToList();

    private async Task<SearchProfile?> ResolveProfileAsync(IReadOnlyList<AutomationSchedule> shiftRows)
    {
        var all = await _profiles.GetAllAsync();
        var wanted = shiftRows.Select(s => s.ProfileId).FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));
        return all.FirstOrDefault(p => p.Id == wanted) ?? all.FirstOrDefault(p => p.IsActive);
    }

    private static ShiftSettingDto ToShiftDto(AutomationSchedule s) => new(
        s.Id, s.Name, s.IsEnabled, s.IntervalHours, s.SelectedSources.ToList(),
        s.QuietHoursEnd, s.QuietHoursStart, s.Timezone);

    private static IReadOnlyList<StoredAgent> ReadStored(AgentSettings? row)
    {
        if (row == null || string.IsNullOrWhiteSpace(row.AgentsJson)) return Array.Empty<StoredAgent>();
        try
        {
            return JsonSerializer.Deserialize<List<StoredAgent>>(row.AgentsJson, AgentLog.Json) ?? new List<StoredAgent>();
        }
        catch (JsonException)
        {
            return Array.Empty<StoredAgent>();
        }
    }

    /// <summary>Keeps the owner's casing for display; matching lowercases separately.</summary>
    private static IReadOnlyList<string> SplitKeywords(string? text) =>
        (text ?? string.Empty).Split(KeywordSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static string Join(IEnumerable<string>? items) =>
        string.Join(", ", (items ?? Array.Empty<string>()).Select(i => i.Trim()).Where(i => i.Length > 0));
}
