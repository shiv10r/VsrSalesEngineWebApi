namespace VsrSalesEngineWebApi.Core.Services.Reliability;

/// <summary>Classifies failures as transient or permanent and computes retry backoff.</summary>
public static class FailureClassifier
{
    public static string Classify(string errorMessage, Exception? ex = null)
    {
        if (ex is TimeoutException ||
            errorMessage.Contains("Timeout", StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("timed out", StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("time budget", StringComparison.OrdinalIgnoreCase))
        {
            return "Timeout";
        }

        if (errorMessage.Contains("429", StringComparison.OrdinalIgnoreCase) || errorMessage.Contains("Rate limit", StringComparison.OrdinalIgnoreCase))
        {
            return "RateLimitExceeded";
        }

        // Transient markers
        string[] transientKeywords = {
            "500", "502", "503", "504", "Bad Gateway", "Gateway Timeout", "Service Unavailable",
            "Connection reset", "SocketException", "temporarily unavailable", "network", "transient"
        };

        if (transientKeywords.Any(k => errorMessage.Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            return "Transient";
        }

        // Permanent markers
        string[] permanentKeywords = {
            "400", "401", "403", "404", "Unauthorized", "Forbidden", "NotFound", "Bad Request",
            "Prohibited", "Suppression", "Exclusion", "Syntax", "Invalid format", "Malformed"
        };

        if (permanentKeywords.Any(k => errorMessage.Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            return "Permanent";
        }

        return "Permanent";
    }

    public static TimeSpan CalculateBackoff(int attempt)
    {
        var baseSeconds = Math.Pow(2, Math.Max(1, attempt));
        var cappedSeconds = Math.Min(60, baseSeconds);
        return TimeSpan.FromSeconds(cappedSeconds);
    }
}
