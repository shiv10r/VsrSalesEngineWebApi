using System.Text.RegularExpressions;
using VsrSalesEngineWebApi.Core.DTOs;

namespace VsrSalesEngineWebApi.Core.Services.Drafting;

/// <summary>Checks an outreach draft for unfilled variables, prohibited claims and missing compliance footer parts.</summary>
public static class DraftValidator
{
    private static readonly string[] ProhibitedClaimsList = new[]
    {
        "100% guarantee",
        "guarantee 100%",
        "guaranteed results",
        "exclusive partner",
        "exclusive partnership",
        "certified government contractor",
        "official partner of your company",
        "we built your current system",
        "we built your existing",
        "we created your platform",
        "proprietary access to your data",
        "internal security backdoor",
        "zero risk",
        "no risk"
    };

    public static DraftValidationResult Validate(DraftContentValidationRequest request)
    {
        var missingVars = new List<string>();
        var prohibitedClaims = new List<string>();
        var warnings = new List<string>();

        // 1. Check for missing/unsubstituted template tags {{Variable}}
        var regex = new Regex(@"\{\{([A-Za-z0-9_]+)\}\}", RegexOptions.Compiled);
        foreach (Match match in regex.Matches(request.Subject + " " + request.Body))
        {
            var varName = match.Groups[1].Value;
            if (!missingVars.Contains(varName))
            {
                missingVars.Add(varName);
            }
        }

        // 2. Check for prohibited / fabricated claims
        var fullText = (request.Subject + " " + request.Body).ToLowerInvariant();
        foreach (var claim in ProhibitedClaimsList)
        {
            if (fullText.Contains(claim.ToLowerInvariant()))
            {
                prohibitedClaims.Add($"Contains prohibited claim: '{claim}'");
            }
        }

        // 3. Excessive repetition check
        var words = Regex.Split(fullText, @"\W+").Where(w => w.Length > 4).ToList();
        var counts = words.GroupBy(w => w).Where(g => g.Count() >= 5).Select(g => g.Key).ToList();
        if (counts.Any())
        {
            warnings.Add($"Excessive word repetition detected for: {string.Join(", ", counts)}");
        }

        // 4. Compliance footer checks
        var hasUnsub = fullText.Contains("unsubscribe") || fullText.Contains("opt-out") || fullText.Contains("opt out") || fullText.Contains("stop");
        var hasAddress = fullText.Contains("100 enterprise way") || fullText.Contains("suite") || fullText.Contains("street") || fullText.Contains("austin");
        var hasSenderIdentity = fullText.Contains("vsr technologies") || fullText.Contains("vsr");

        if (!hasUnsub)
        {
            warnings.Add("Missing required opt-out/unsubscribe instructions in footer.");
        }
        if (!hasAddress)
        {
            warnings.Add("Missing required physical business mailing address in footer.");
        }
        if (!hasSenderIdentity)
        {
            warnings.Add("Missing clear sender commercial identity.");
        }

        var isValid = missingVars.Count == 0 && prohibitedClaims.Count == 0 && hasUnsub && hasAddress;

        return new DraftValidationResult(
            IsValid: isValid,
            MissingVariables: missingVars,
            ProhibitedClaims: prohibitedClaims,
            Warnings: warnings,
            HasUnsubscribeMechanism: hasUnsub,
            HasPhysicalAddress: hasAddress,
            HasSenderIdentity: hasSenderIdentity
        );
    }
}
