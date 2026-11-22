using System.Text.RegularExpressions;

namespace VsrSalesEngineWebApi.Core.Services.AgentRelay.People;

public record TitleMatch(int Index, int Length, string Text, string Relation);

/// <summary>Recognises job titles and says how that person relates to a hiring post.</summary>
public static class TitleClassifier
{
    private static Regex Pattern(string alternatives) =>
        new($@"\b(?:{alternatives})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly (string Relation, Regex Regex)[] Patterns =
    {
        (PersonRelation.DecisionMaker, Pattern(
            @"co[- ]?founders?|founders?|ceo|chief\s+executive\s+officer|coo|chief\s+operating\s+officer|cto|" +
            @"chief\s+(?:technology|technical)\s+officer|head\s+of\s+(?:engineering|technology)|" +
            @"(?:vp|vice\s+president)\s+(?:of\s+)?(?:engineering|technology)|director\s+of\s+(?:engineering|technology)|" +
            @"(?:engineering|technology)\s+director")),
        (PersonRelation.Hiring, Pattern(
            @"hiring\s+manager|hiring\s+lead|(?:technical\s+|tech\s+)?recruiter|" +
            @"talent\s+acquisition(?:\s+(?:partner|specialist|lead|manager))?|head\s+of\s+people|" +
            @"people\s+(?:operations|partner)|people\s+(?:&|and)\s+culture|" +
            @"hr(?:\s+(?:manager|business\s+partner|lead))?|human\s+resources(?:\s+manager)?")),
        (PersonRelation.Engineering, Pattern(
            @"engineering\s+manager|(?:tech|technical|team)\s+lead|(?:principal|staff)\s+(?:software\s+)?engineer|" +
            @"head\s+of\s+product|product\s+manager"))
    };

    /// <summary>All titles in the text, left to right, without overlaps (a longer match wins).</summary>
    public static IReadOnlyList<TitleMatch> Find(string text)
    {
        var all = new List<TitleMatch>();
        foreach (var (relation, regex) in Patterns)
        {
            foreach (Match m in regex.Matches(text)) all.Add(new TitleMatch(m.Index, m.Length, m.Value, relation));
        }

        var kept = new List<TitleMatch>();
        foreach (var m in all.OrderBy(x => x.Index).ThenByDescending(x => x.Length))
        {
            if (kept.Count > 0 && m.Index < kept[^1].Index + kept[^1].Length) continue;
            kept.Add(m);
        }
        return kept;
    }

    /// <summary>The best-ranked relation among the matches and the titles joined for display.</summary>
    public static (string Relation, string Title) Summarise(IReadOnlyList<TitleMatch> matches)
    {
        var relation = matches.OrderBy(m => PersonRelation.Rank(m.Relation)).First().Relation;
        var titles = matches.Select(m => string.Join(' ', m.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries)))
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(3);
        var title = string.Join(" & ", titles);
        return (relation, title.Length > 80 ? title[..80] : title);
    }

    /// <summary>The text with every title replaced by a separator, so what is left can be tested for a name.</summary>
    public static string WithoutTitles(string text, IReadOnlyList<TitleMatch> matches)
    {
        var result = text;
        foreach (var m in matches.OrderByDescending(x => x.Index)) result = result.Remove(m.Index, m.Length).Insert(m.Index, " | ");
        return result;
    }
}
