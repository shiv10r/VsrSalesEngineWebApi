using System.Net;
using System.Text.RegularExpressions;
using VsrSalesEngineWebApi.Core.Entities;

namespace VsrSalesEngineWebApi.Core.Services.AgentRelay.People;

/// <summary>Finds a company's own website from data we already hold. It never searches the web.</summary>
public static class CompanySiteLocator
{
    private static readonly Regex UrlInText = new(@"https?://[^\s<>""'\)\]]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>The company website field if the feed gave one, else the posting's link, else a link inside the post text.</summary>
    public static Uri? FromOpportunity(Opportunity opportunity)
    {
        return FromUrl(opportunity.CompanyUrl)
            ?? FromUrl(opportunity.SourceUrl)
            ?? FromText(opportunity.PostingText)
            ?? FromText(opportunity.EvidenceSnippet);
    }

    /// <summary>The https origin of a URL when it is not a job board, social site, shortener or IP address.</summary>
    public static Uri? FromUrl(string? value)
    {
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var url)) return null;
        if (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps) return null;

        var host = url.IdnHost.ToLowerInvariant();
        if (!host.Contains('.') || IPAddress.TryParse(host, out _)) return null;
        if (JobBoardHosts.IsJobBoardOrAggregator(host)) return null;

        return new Uri($"https://{host}/");
    }

    /// <summary>The first link in some text that points to a company's own site.</summary>
    public static Uri? FromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        foreach (Match match in UrlInText.Matches(text))
        {
            var site = FromUrl(match.Value.TrimEnd('.', ',', ';', ':', '!', '?'));
            if (site != null) return site;
        }
        return null;
    }
}
