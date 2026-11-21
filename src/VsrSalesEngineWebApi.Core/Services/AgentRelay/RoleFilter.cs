using System.Text.RegularExpressions;

namespace VsrSalesEngineWebApi.Core.Services.AgentRelay;

public record FilterDecision(bool Keep, string Reason);

/// <summary>Decides whether a posting is software work worth keeping. Looks at the title only, which is the reliable part of every feed.</summary>
public static class RoleFilter
{
    private static readonly Regex SoftwareTerms = new(
        @"(\bdeveloper\b|\bengineer\b|\bengineering\b|\bsoftware\b|\bprogrammer\b|\bdevops\b|\bsre\b|\barchitect\b|" +
        @"\bfull[- ]?stack\b|\bfront[- ]?end\b|\bback[- ]?end\b|\.net\b|\bdotnet\b|\bc#|\bpython\b|\bjava\b|\bjavascript\b|" +
        @"\btypescript\b|\breact\b|\bnode(\.js)?\b|\bgolang\b|\brust\b|\bphp\b|\bruby\b|\bandroid\b|\bios\b|\bmobile\b|" +
        @"\bqa\b|\bsdet\b|\bapi\b|\bcloud\b|\bwordpress\b|\bshopify\b|\bweb dev)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static FilterDecision Evaluate(string title, IReadOnlyList<string> excludedKeywords)
    {
        var lowered = title.ToLowerInvariant();

        foreach (var excluded in excludedKeywords)
        {
            if (lowered.Contains(excluded)) return new FilterDecision(false, $"matches your excluded keyword '{excluded}'");
        }

        if (!SoftwareTerms.IsMatch(title))
        {
            return new FilterDecision(false, "not a software role (nothing in the title points to development or engineering)");
        }

        return new FilterDecision(true, "software role");
    }
}
