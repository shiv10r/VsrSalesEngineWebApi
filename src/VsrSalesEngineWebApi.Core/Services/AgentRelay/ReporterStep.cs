using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Core.Services.AgentRelay;

/// <summary>Step 8: writes the run summary and counts onto the run record.</summary>
public class ReporterStep : IRelayStep
{
    private const string Agent = AgentIds.Reporter;
    public string AgentId => Agent;

    private readonly IAgentRunRepository _runs;

    public ReporterStep(IAgentRunRepository runs)
    {
        _runs = runs;
    }

    public async Task RunAsync(RelayState state, CancellationToken cancellationToken)
    {
        var run = state.Run;
        run.NewLeads = state.NewLeads;
        run.Duplicates = state.Duplicates;
        run.Rejected = state.Rejected;
        run.DraftsReady = state.DraftsReady;
        run.FinishedAtUtc = DateTime.UtcNow;

        var noSourceReadable = state.SourcesReached == 0 && state.SourcesFailed > 0;
        run.Status = noSourceReadable ? "failed" : "completed";

        var duration = FormatDuration(run.FinishedAtUtc.Value - run.StartedAtUtc);
        var nothingToDo = state.SourcesReached == 0 && state.SourcesFailed == 0;
        run.Summary = nothingToDo
            ? $"{run.ShiftName} had nothing to do: no enabled source to read" +
              (state.SourcesSkipped > 0 ? $" ({state.SourcesSkipped} selected source(s) skipped: switched off or no adapter)." : " (none selected).") +
              " Nothing was saved."
            : noSourceReadable
            ? $"{run.ShiftName} could not read any source ({state.SourcesFailed} failed). Nothing was saved."
            : $"{run.ShiftName} finished in {duration}: read {state.PostingsRead} posting(s) from {state.SourcesReached} source(s), " +
              $"dropped {state.Rejected} as not relevant, skipped {state.Duplicates} already stored, saved {state.NewLeads} new lead(s), " +
              $"{state.DraftsReady} draft(s) ready for your approval. Nothing was sent.";

        await _runs.UpdateRunAsync(run);
        await state.Log.ResultAsync(Agent, run.Summary);
    }

    private static string FormatDuration(TimeSpan span) =>
        span.TotalMinutes >= 1 ? $"{(int)span.TotalMinutes}m {span.Seconds}s" : $"{Math.Max(1, (int)span.TotalSeconds)}s";
}
