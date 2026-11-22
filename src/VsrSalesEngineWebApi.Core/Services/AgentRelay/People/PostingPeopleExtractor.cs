using System.Text.RegularExpressions;
using VsrSalesEngineWebApi.Core.Entities;

namespace VsrSalesEngineWebApi.Core.Services.AgentRelay.People;

/// <summary>
/// People we can find without fetching anything: the poster (from data the feed already gave us) and anyone
/// the post text itself names. Uses only the post, never another website.
/// </summary>
public static class PostingPeopleExtractor
{
    private const string Capitalised = @"\p{Lu}[\p{L}'’\-]*";

    private static readonly Regex Mention = new(
        @"(?i:\b(?:contact|e-?mail|reach\s+out\s+to|ask\s+for|message|dm|ping|apply\s+to|talk\s+to|speak\s+(?:to|with)|attn|attention))" +
        $@"\s*:?\s+(?<n>{Capitalised}(?:\s+{Capitalised}){{0,2}})",
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly Regex Sentences = new(@"(?<=[.!?])\s+|\r?\n+", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    public static IReadOnlyList<RelatedPerson> Extract(Opportunity opportunity)
    {
        var people = new List<RelatedPerson>();

        if (!string.IsNullOrWhiteSpace(opportunity.PosterHandle))
        {
            var handle = opportunity.PosterHandle.Trim();
            people.Add(new RelatedPerson(handle, "Poster", PersonRelation.Poster, opportunity.SourceUrl, "high", null,
                Evidence: $"Published this post as '{handle}'.", Handle: handle));
        }

        var text = !string.IsNullOrWhiteSpace(opportunity.PostingText) ? opportunity.PostingText : opportunity.EvidenceSnippet;
        if (string.IsNullOrWhiteSpace(text)) return people;

        foreach (var sentence in Sentences.Split(text).Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            foreach (var part in sentence.Split('|', StringSplitOptions.TrimEntries))
            {
                if (PagePeopleExtractor.TrySameLine(part, out var name, out var title, out var relation))
                {
                    people.Add(new RelatedPerson(name, title, relation, opportunity.SourceUrl, "high", null, Short(sentence)));
                }
            }

            foreach (Match mention in Mention.Matches(sentence))
            {
                var mentioned = mention.Groups["n"].Value.Trim();
                if (!IsMentionableName(mentioned, opportunity.PosterHandle)) continue;
                if (people.Any(p => p.Relation != PersonRelation.Poster && PersonNames.Key(p.Name) == PersonNames.Key(mentioned))) continue;

                people.Add(new RelatedPerson(mentioned, "Named in the post", PersonRelation.Mentioned,
                    opportunity.SourceUrl, "medium", null, Short(sentence)));
            }
        }

        return people;
    }

    private static bool IsMentionableName(string name, string? posterHandle)
    {
        var tokens = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Any(PersonNames.IsStopWord)) return false;
        if (tokens.Any(t => t.Length < 3 && !t.EndsWith('.'))) return false;
        if (tokens.Any(t => !t.Skip(1).Any(char.IsLower))) return false;     // "HR", "CEO": not a name
        return !string.Equals(PersonNames.Key(name), PersonNames.Key(posterHandle ?? string.Empty), StringComparison.Ordinal);
    }

    private static string Short(string sentence) => sentence.Length > 200 ? sentence[..200] + "..." : sentence;
}
