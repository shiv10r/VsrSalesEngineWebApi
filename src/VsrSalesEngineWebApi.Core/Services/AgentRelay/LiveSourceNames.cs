namespace VsrSalesEngineWebApi.Core.Services.AgentRelay;

/// <summary>The sources that have a real live reader. Other sources are never offered to the scout.</summary>
public static class LiveSourceNames
{
    public static bool IsLive(string name) =>
        name.Contains("Hacker", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("We Work", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Remotive", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Jobicy", StringComparison.OrdinalIgnoreCase);
}
