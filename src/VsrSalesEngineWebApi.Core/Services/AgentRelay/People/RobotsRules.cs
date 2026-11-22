using System.Text.RegularExpressions;

namespace VsrSalesEngineWebApi.Core.Services.AgentRelay.People;

/// <summary>A small robots.txt reader: our user agent's group wins over "*", the longest matching rule wins, ties allow.</summary>
public sealed class RobotsRules
{
    private sealed record Rule(bool Allow, int Length, Regex Pattern);

    private readonly IReadOnlyList<Rule> _rules;
    private RobotsRules(IReadOnlyList<Rule> rules) => _rules = rules;

    public static RobotsRules AllowAll { get; } = new(Array.Empty<Rule>());

    public static RobotsRules Parse(string text, string userAgentToken)
    {
        var groups = new List<(List<string> Agents, List<(bool Allow, string Path)> Rules)>();
        List<string>? agents = null;
        var lastWasAgent = false;

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Split('#')[0].Trim();
            var colon = line.IndexOf(':');
            if (colon <= 0) continue;
            var field = line[..colon].Trim().ToLowerInvariant();
            var value = line[(colon + 1)..].Trim();

            if (field == "user-agent")
            {
                if (!lastWasAgent || agents == null)
                {
                    agents = new List<string>();
                    groups.Add((agents, new List<(bool, string)>()));
                }
                agents.Add(value.ToLowerInvariant());
                lastWasAgent = true;
                continue;
            }

            lastWasAgent = false;
            if (agents == null || (field != "allow" && field != "disallow")) continue;
            if (value.Length == 0) continue;                      // "Disallow:" with no path allows everything
            groups[^1].Rules.Add((field == "allow", value));
        }

        var token = userAgentToken.ToLowerInvariant();
        var specific = groups.Where(g => g.Agents.Any(a => a != "*" && token.StartsWith(a, StringComparison.Ordinal))).ToList();
        var chosen = specific.Count > 0 ? specific : groups.Where(g => g.Agents.Contains("*")).ToList();

        return new RobotsRules(chosen.SelectMany(g => g.Rules).Select(r => new Rule(r.Allow, r.Path.Length, ToRegex(r.Path))).ToList());
    }

    public bool IsAllowed(string pathAndQuery)
    {
        var path = string.IsNullOrEmpty(pathAndQuery) ? "/" : pathAndQuery;
        Rule? best = null;
        foreach (var rule in _rules)
        {
            if (!rule.Pattern.IsMatch(path)) continue;
            if (best == null || rule.Length > best.Length || (rule.Length == best.Length && rule.Allow)) best = rule;
        }
        return best?.Allow ?? true;
    }

    private static Regex ToRegex(string pattern)
    {
        var endAnchor = pattern.EndsWith('$');
        if (endAnchor) pattern = pattern[..^1];
        var body = string.Join(".*", pattern.Split('*').Select(Regex.Escape));
        return new Regex("^" + body + (endAnchor ? "$" : string.Empty), RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    }
}
