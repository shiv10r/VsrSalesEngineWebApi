namespace VsrSalesEngineWebApi.Core.Services.AgentRelay;

/// <summary>The settings one run uses. Required agents are always on, whatever is stored.</summary>
public sealed class AgentRuntimeSettings
{
    private readonly IReadOnlyDictionary<string, string> _names;
    private readonly IReadOnlySet<string> _disabled;

    public AgentRuntimeSettings(IReadOnlyDictionary<string, string> names, IReadOnlySet<string> disabled, int draftThreshold)
    {
        _names = names;
        _disabled = disabled;
        DraftThreshold = draftThreshold;
    }

    public int DraftThreshold { get; }

    public string NameOf(string agentId) => _names.TryGetValue(agentId, out var name) ? name : AgentCatalog.NameOf(agentId);

    public bool IsEnabled(string agentId) => AgentCatalog.IsRequired(agentId) || !_disabled.Contains(agentId);

    public static AgentRuntimeSettings Defaults(int draftThreshold) =>
        new(AgentCatalog.All.ToDictionary(a => a.Id, a => a.Name), new HashSet<string>(), draftThreshold);
}
