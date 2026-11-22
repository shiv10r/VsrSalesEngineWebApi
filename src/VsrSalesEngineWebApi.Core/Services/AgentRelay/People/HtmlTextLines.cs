using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace VsrSalesEngineWebApi.Core.Services.AgentRelay.People;

/// <summary>One visible line of a page. Mailtos are the mailto links that sit inside it.</summary>
public record TextLine(string Text, IReadOnlyList<string> Mailtos);

/// <summary>
/// Turns HTML into visible text lines without running scripts. Block elements start a new line; inline
/// elements do not, so "Name <span>CTO</span>" stays one line while a name and title in separate blocks do not.
/// </summary>
public static class HtmlTextLines
{
    private const RegexOptions Opts = RegexOptions.CultureInvariant;
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(2);

    private static readonly Regex Hidden = new(@"<(script|style|noscript|template|svg|head)\b[^>]*>.*?</\1\s*>",
        Opts | RegexOptions.IgnoreCase | RegexOptions.Singleline, Limit);
    private static readonly Regex Comments = new(@"<!--.*?-->", Opts | RegexOptions.Singleline, Limit);
    private static readonly Regex Tag = new(@"<(/?)([a-zA-Z][a-zA-Z0-9]*)\b([^>]{0,2000})>", Opts, Limit);
    private static readonly Regex Mailto = new(@"href\s*=\s*[""']?mailto:([^""'\s>?]+)", Opts | RegexOptions.IgnoreCase, Limit);
    private static readonly Regex Spaces = new(@"\s+", Opts, Limit);

    private static readonly HashSet<string> Breaks = new(StringComparer.OrdinalIgnoreCase)
    {
        "p", "div", "li", "ul", "ol", "br", "tr", "td", "th", "table", "section", "article", "header", "footer",
        "h1", "h2", "h3", "h4", "h5", "h6", "dt", "dd", "blockquote", "figure", "figcaption", "main", "aside", "nav", "hr", "body"
    };

    public static IReadOnlyList<TextLine> Extract(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return Array.Empty<TextLine>();

        html = Comments.Replace(Hidden.Replace(html, " "), " ");
        var lines = new List<TextLine>();
        var buffer = new StringBuilder();
        var mailtos = new List<string>();
        var position = 0;

        void Flush()
        {
            var text = Spaces.Replace(WebUtility.HtmlDecode(buffer.ToString()), " ").Trim();
            if (text.Length > 0 && lines.Count < 3000) lines.Add(new TextLine(text, mailtos.ToArray()));
            buffer.Clear();
            mailtos.Clear();
        }

        foreach (Match tag in Tag.Matches(html))
        {
            if (tag.Index > position) buffer.Append(html, position, tag.Index - position);
            position = tag.Index + tag.Length;

            var name = tag.Groups[2].Value;
            var closing = tag.Groups[1].Value == "/";
            if (!closing && name.Equals("a", StringComparison.OrdinalIgnoreCase))
            {
                var mail = Mailto.Match(tag.Groups[3].Value);
                if (mail.Success) mailtos.Add(WebUtility.UrlDecode(mail.Groups[1].Value).Trim());
            }

            if (Breaks.Contains(name)) Flush();
            else buffer.Append(' ');
        }

        if (position < html.Length) buffer.Append(html, position, html.Length - position);
        Flush();
        return lines;
    }
}
