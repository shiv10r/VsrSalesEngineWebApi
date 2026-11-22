using System.Text.RegularExpressions;

namespace VsrSalesEngineWebApi.Core.Services.AgentRelay.People;

/// <summary>
/// Pulls people out of visible text lines: a name and a recognised title on the same line (high confidence)
/// or on adjacent lines (medium). Never guesses a name, never builds an email address.
/// </summary>
public static class PagePeopleExtractor
{
    private static readonly Regex Separators = new(@"\s*(?:[|,•·:;/]|\s[-–—]\s)\s*", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex EmailShape = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    public static IReadOnlyList<RelatedPerson> Extract(IReadOnlyList<TextLine> lines, string sourceUrl)
    {
        var found = new List<RelatedPerson>();

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];

            if (TrySameLine(line.Text, out var name, out var title, out var relation))
            {
                found.Add(new RelatedPerson(name, title, relation, sourceUrl, "high", PersonEmail(line.Mailtos), line.Text));
                continue;
            }

            if (i + 1 >= lines.Count) continue;
            var next = lines[i + 1];

            if (PersonNames.IsName(line.Text) && TryTitleOnly(next.Text, out title, out relation))
            {
                found.Add(new RelatedPerson(line.Text.Trim(), title, relation, sourceUrl, "medium",
                    PersonEmail(line.Mailtos.Concat(next.Mailtos).ToList()), $"{line.Text} / {next.Text}"));
            }
            else if (TryTitleOnly(line.Text, out title, out relation) && PersonNames.IsName(next.Text))
            {
                found.Add(new RelatedPerson(next.Text.Trim(), title, relation, sourceUrl, "medium",
                    PersonEmail(line.Mailtos.Concat(next.Mailtos).ToList()), $"{line.Text} / {next.Text}"));
            }
        }

        return found;
    }

    /// <summary>"Priya Sharma, CTO" - a short line holding one name and at least one title.</summary>
    public static bool TrySameLine(string text, out string name, out string title, out string relation)
    {
        name = title = relation = string.Empty;
        if (text.Length > 120) return false;

        var matches = TitleClassifier.Find(text);
        if (matches.Count == 0) return false;

        var rest = TitleClassifier.WithoutTitles(text, matches);
        var candidate = Separators.Split(rest).Select(s => NameIn(s.Trim())).FirstOrDefault(n => n != null);
        if (candidate == null) return false;

        name = candidate;
        (relation, title) = TitleClassifier.Summarise(matches);
        return true;
    }

    private static readonly HashSet<string> Cues = new(StringComparer.OrdinalIgnoreCase)
    {
        "talk", "to", "speak", "with", "ask", "for", "contact", "email", "e-mail", "meet", "message", "reach", "out",
        "our", "cc", "attn", "attention", "call", "ping", "dm", "from", "by", "hi", "hello"
    };

    /// <summary>The name in a segment, allowing a few cue words in front ("Talk to Meera Jain").</summary>
    private static string? NameIn(string segment)
    {
        if (PersonNames.IsName(segment)) return segment;

        var tokens = segment.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var take = Math.Min(3, tokens.Length - 1); take >= 2; take--)
        {
            var head = tokens[..^take];
            var tail = string.Join(' ', tokens[^take..]);
            if (head.All(t => Cues.Contains(t.Trim(':', ','))) && PersonNames.IsName(tail)) return tail;
        }
        return null;
    }

    /// <summary>A short line that is only a title, such as "CTO" or "Technical Recruiter at Acme".</summary>
    public static bool TryTitleOnly(string text, out string title, out string relation)
    {
        title = relation = string.Empty;
        if (text.Length > 60) return false;

        var matches = TitleClassifier.Find(text);
        if (matches.Count == 0) return false;

        var rest = TitleClassifier.WithoutTitles(text, matches);
        if (Separators.Split(rest).Any(s => PersonNames.IsName(s))) return false;
        if (rest.Count(char.IsLetter) > 25) return false;

        (relation, title) = TitleClassifier.Summarise(matches);
        return true;
    }

    /// <summary>A mailto that sits inside the person's own block. Shared mailboxes (info@) are never attached to a person.</summary>
    private static string? PersonEmail(IReadOnlyList<string> mailtos) =>
        mailtos.FirstOrDefault(m => EmailShape.IsMatch(m) && !RoleAccountClassifier.IsRoleAccount(m));
}
