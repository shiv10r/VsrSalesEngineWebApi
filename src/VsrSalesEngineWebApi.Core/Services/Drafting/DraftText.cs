namespace VsrSalesEngineWebApi.Core.Services.Drafting;

/// <summary>Compliance footer text per jurisdiction and prompt-injection-safe source snippets.</summary>
public static class DraftText
{
    public static string DefaultFooter(string jurisdiction)
    {
        return jurisdiction.ToUpperInvariant() switch
        {
            "EU-GDPR" =>
                "You are receiving this B2B communication on the basis of legitimate interest related to your public professional role. To exercise your right to object or opt out at any time, reply 'UNSUBSCRIBE' or visit {{OptOutUrl}}.\nController: {{SenderCompany}}, 100 Enterprise Way, Suite 400, Austin TX 78701.",
            "UK-PECR" =>
                "This message is intended for corporate subscribers under UK PECR regulation. If you wish to opt out of further business communications, reply 'STOP' or visit {{OptOutUrl}}.\n{{SenderCompany}}, 100 Enterprise Way, Suite 400, Austin TX 78701.",
            _ =>
                "To opt out of future updates, reply 'UNSUBSCRIBE' or visit {{OptOutUrl}}.\n{{SenderCompany}}, 100 Enterprise Way, Suite 400, Austin TX 78701."
        };
    }

    public static string SanitizeSourceSnippet(string? snippet)
    {
        if (string.IsNullOrWhiteSpace(snippet)) return "your public technical requirements";
        var sanitized = snippet.Trim();
        string[] injectionPatterns = {
            "ignore all previous instructions",
            "ignore previous instructions",
            "output the api",
            "system prompt",
            "output secret"
        };

        foreach (var pattern in injectionPatterns)
        {
            if (sanitized.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return "your public technical requirements";
            }
        }

        return sanitized;
    }
}
