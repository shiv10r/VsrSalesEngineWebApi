using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Core.Services.AgentRelay;

/// <summary>Runs the six agents in order for one run and keeps the run record up to date.</summary>
public class AgentRelayService : IAgentRelay
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(30);

    private readonly IAgentRunRepository _runs;
    private readonly ISearchProfileRepository _profiles;
    private readonly ISourceRegistryRepository _sources;
    private readonly AgentRelayOptions _options;
    private readonly IReadOnlyList<IRelayStep> _steps;
    private readonly ScorerStep _scorer;
    private readonly ReporterStep _reporter;
    private readonly IAgentSettingsService? _settings;

    public AgentRelayService(
        IAgentRunRepository runs,
        ISearchProfileRepository profiles,
        ISourceRegistryRepository sources,
        IOpportunityRepository opportunities,
        IContactRepository contacts,
        AgentRelayOptions options,
        ILiveJobFeedService? feeds = null,
        IAgentSettingsService? settings = null,
        IRelatedPeopleFinder? relatedPeople = null)
    {
        _runs = runs;
        _profiles = profiles;
        _sources = sources;
        _options = options;
        _settings = settings;
        _scorer = new ScorerStep(opportunities);

        _steps = new IRelayStep[]
        {
            new ScoutStep(feeds, sources),
            new FilterStep(),
            new DedupeStep(opportunities),
            _scorer,
            new ContactFinderStep(contacts, relatedPeople, options.MaxSiteLookups)
        };
        _reporter = new ReporterStep(runs);
    }

    public async Task<AgentRun?> BeginRunAsync(RelayRequest request)
    {
        await _runs.FailStaleRunsAsync(DateTime.UtcNow - StaleAfter);
        if (await _runs.GetRunningAsync(DateTime.UtcNow - StaleAfter) != null) return null;

        return await _runs.AddRunAsync(new AgentRun
        {
            Id = $"RUN-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}",
            ScheduleId = request.ScheduleId,
            ShiftName = request.ShiftName,
            Trigger = request.Trigger,
            StartedAtUtc = DateTime.UtcNow,
            Status = "running"
        });
    }

    public async Task<AgentRun?> RunAsync(RelayRequest request, CancellationToken cancellationToken = default)
    {
        var run = await BeginRunAsync(request);
        return run == null ? null : await ExecuteAsync(run.Id, request, cancellationToken);
    }

    public async Task<AgentRun> ExecuteAsync(string runId, RelayRequest request, CancellationToken cancellationToken = default)
    {
        var run = await _runs.GetRunAsync(runId) ?? throw new InvalidOperationException($"Run '{runId}' not found.");
        var log = new AgentLog(_runs, run.Id, _options.StepPauseMs);
        var state = new RelayState(run, request, log, _options);

        try
        {
            state.Runtime = _settings != null
                ? await _settings.GetRuntimeAsync()
                : AgentRuntimeSettings.Defaults();
            await LoadProfileAsync(state, cancellationToken);

            foreach (var step in _steps)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!state.IsEnabled(step.AgentId))
                {
                    await SkipSwitchedOffAsync(state, step, cancellationToken);
                    continue;
                }

                await step.RunAsync(state, cancellationToken);
            }

            await _reporter.RunAsync(state, cancellationToken);
        }
        catch (Exception ex)
        {
            run.Status = "failed";
            run.FinishedAtUtc = DateTime.UtcNow;
            run.Summary = $"{request.ShiftName} stopped with an error: {KeywordText.Short(ex.Message, 160)}";
            run.NewLeads = state.NewLeads;
            run.Duplicates = state.Duplicates;
            run.Rejected = state.Rejected;
            await _runs.UpdateRunAsync(run);
            await log.WarningAsync(AgentIds.Reporter, run.Summary);
        }

        return run;
    }

    public async Task RecordSkippedAsync(RelayRequest request, string reason)
    {
        var now = DateTime.UtcNow;
        var run = await _runs.AddRunAsync(new AgentRun
        {
            Id = $"RUN-{now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}",
            ScheduleId = request.ScheduleId,
            ShiftName = request.ShiftName,
            Trigger = request.Trigger,
            StartedAtUtc = now,
            FinishedAtUtc = now,
            Status = "skipped",
            Summary = reason
        });

        await new AgentLog(_runs, run.Id, 0).EmitAsync(AgentIds.Reporter, EventKinds.Thought, reason);
    }

    public async Task<RelayRequest> BuildManualRequestAsync(string user)
    {
        var live = (await _sources.GetAllSourcesAsync())
            .Where(s => s.PolicyStatus == "Active" && s.Category == "ContractBoard")
            .Select(s => s.Name)
            .ToList();

        var profile = (await _profiles.GetAllAsync()).FirstOrDefault(p => p.IsActive);
        return new RelayRequest("MANUAL", $"Manual run by {user}", profile?.Id, live, 50, "Manual");
    }

    /// <summary>One event says the agent is off and what that means for this run.</summary>
    private async Task SkipSwitchedOffAsync(RelayState state, IRelayStep step, CancellationToken ct)
    {
        var effect = step.AgentId switch
        {
            AgentIds.Filter => "Keeping every posting.",
            AgentIds.Scorer => $"Saved {await _scorer.SaveUnscoredAsync(state, ct)} lead(s) without scores.",
            AgentIds.ContactFinder => "No contact search this run.",
            _ => string.Empty
        };

        await state.Log.ThoughtAsync(step.AgentId, $"{state.NameOf(step.AgentId)} is switched off - skipping this step. {effect}".Trim());
    }

    private async Task LoadProfileAsync(RelayState state, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(state.Request.ProfileId))
        {
            state.Profile = await _profiles.GetByIdAsync(state.Request.ProfileId, ct);
        }

        var targets = KeywordText.Split(state.Profile?.TargetKeywords);
        state.TargetKeywords = targets.Count > 0 ? targets : new[] { ".net", "c#" };
        state.ExcludedKeywords = KeywordText.Split(state.Profile?.ExcludedKeywords);
    }
}
