using System.Text.RegularExpressions;

namespace VsrSalesEngineWebApi.Core.Services.AgentRelay;

public static class KeywordText
{
    private static readonly Regex EmailPattern =
        new(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", RegexOptions.Compiled);

    /// <summary>Splits a profile keyword string such as "ASP.NET, React; Azure" into lower-cased phrases.</summary>
    public static IReadOnlyList<string> Split(string? keywords)
    {
        if (string.IsNullOrWhiteSpace(keywords)) return Array.Empty<string>();

        return keywords
            .Split(new[] { ',', ';', '|', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(k => k.ToLowerInvariant())
            .Where(k => k.Length >= 2)
            .Distinct()
            .ToList();
    }

    public static string? FindEmail(params string?[] texts)
    {
        foreach (var text in texts)
        {
            if (string.IsNullOrWhiteSpace(text)) continue;
            var match = EmailPattern.Match(text);
            if (match.Success) return match.Value.TrimEnd('.');
        }
        return null;
    }

    public static string Short(string text, int max = 60) =>
        text.Length <= max ? text : text[..(max - 1)].TrimEnd() + "...";
}
