using System.Text.RegularExpressions;

namespace VsrSalesEngineWebApi.Infrastructure.Services.LiveFeeds;

/// <summary>Text helpers shared by the live job feed readers.</summary>
internal static class FeedText
{
    public static readonly Regex EmailRegex = new(@"[a-zA-Z0-9_.+-]+@[a-zA-Z0-9-]+\.[a-zA-Z0-9-.]+", RegexOptions.Compiled);
    private static readonly Regex HtmlTagRegex = new(@"<[^>]+>", RegexOptions.Compiled);

    public static string CleanHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var decoded = System.Net.WebUtility.HtmlDecode(html);
        var noTags = HtmlTagRegex.Replace(decoded, " ");
        return Regex.Replace(noTags, @"\s+", " ").Trim();
    }
}
