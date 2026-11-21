using System.Text.RegularExpressions;
using VsrSalesEngineWebApi.Core.Entities;

namespace VsrSalesEngineWebApi.Core.Services.AgentRelay;

public record ScoreResult(int Score, List<string> Reasons)
{
    public string Fit => Score >= 70 ? "High" : Score >= 45 ? "Medium" : "Low";
}

/// <summary>
/// Transparent lead scoring: every point comes from a listed rule, so the score always equals the sum of
/// the reasons. Rule-based only (no AI). Posting age is not scored because the feeds do not give a reliable date.
/// </summary>
public static class ScoringRules
{
    private const int ContractPoints = 20;
    private const int CoreStackPoints = 25;
    private const int ModernStackPoints = 10;
    private const int KeywordPoints = 10;
    private const int MaxKeywordMatches = 2;
    private const int EmailPoints = 15;
    private const int RemotePoints = 5;

    private static readonly Regex Contract =
        new(@"\b(contract|contractor|freelance|consultant|fixed[- ]term|project[- ]based)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CoreStack =
        new(@"(\.net\b|\bdotnet\b|\bc#|\basp\.net\b|\bblazor\b|\bentity framework\b)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ModernStack =
        new(@"\b(react|typescript|node(\.js)?|azure|aws|api|full[- ]?stack|back[- ]?end|cloud|kubernetes|docker)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static ScoreResult Score(Opportunity opportunity, IReadOnlyList<string> profileKeywords, bool hasPublicEmail)
    {
        var text = $"{opportunity.Title} {opportunity.EvidenceSnippet}";
        var reasons = new List<string>();
        var total = 0;

        void Add(int points, string label)
        {
            total += points;
            reasons.Add($"{label} (+{points})");
        }

        if (Contract.IsMatch(text)) Add(ContractPoints, "Contract engagement");
        if (CoreStack.IsMatch(text)) Add(CoreStackPoints, ".NET / C# match");

        var modern = ModernStack.Match(text);
        if (modern.Success) Add(ModernStackPoints, $"Modern stack match ({modern.Value.ToLowerInvariant()})");

        var lowered = text.ToLowerInvariant();
        foreach (var keyword in profileKeywords.Where(k => lowered.Contains(k)).Take(MaxKeywordMatches))
        {
            Add(KeywordPoints, $"Matches your keyword '{keyword}'");
        }

        if (hasPublicEmail) Add(EmailPoints, "Public contact email in the posting");

        var remoteText = $"{opportunity.Location} {text}";
        if (remoteText.Contains("remote", StringComparison.OrdinalIgnoreCase)) Add(RemotePoints, "Remote-friendly");

        return new ScoreResult(total, reasons);
    }
}
