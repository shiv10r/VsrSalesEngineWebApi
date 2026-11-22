using System.Text.RegularExpressions;
using VsrSalesEngineWebApi.Core.Entities;

namespace VsrSalesEngineWebApi.Core.Services.LocalLeads;

public record LeadInput(
    string? BusinessName,
    string? District,
    string? Category,
    string? Address,
    string? Phone,
    string? Website,
    double? Rating,
    int? ReviewCount,
    string? Notes);

/// <summary>Validates raw lead fields and turns them into a normalised entity, or returns the first error.</summary>
public static class LocalLeadBuilder
{
    public const string DistrictError = "district missing or not in Uttar Pradesh";
    public const int MaxNameLength = 200;

    public static readonly IReadOnlyList<string> Statuses = new[] { "New", "Contacted", "Interested", "Won", "Lost" };

    public static (LocalLead? Lead, string? Error) Build(LeadInput input, string source, string sourceLabel, string actor)
    {
        var name = (input.BusinessName ?? string.Empty).Trim();
        if (name.Length == 0) return Fail("businessName is required");
        if (name.Length > MaxNameLength) return Fail($"businessName is too long (max {MaxNameLength} characters)");

        var address = (input.Address ?? string.Empty).Trim();
        var district = UttarPradesh.Resolve(input.District) ?? UttarPradesh.DetectInAddress(address);
        if (district is null) return Fail(DistrictError);

        if (!TryPhone(input.Phone, out var phone, out var normalizedPhone)) return Fail("phone must have 10 digits");
        if (!TryWebsite(input.Website, out var website)) return Fail("website is not a valid URL");
        if (input.Rating is { } r && (r < 0 || r > 5 || double.IsNaN(r))) return Fail("rating must be between 0 and 5");
        if (input.ReviewCount is < 0) return Fail("reviewCount cannot be negative");

        var (category, reason) = ChooseCategory(name, input.Category);
        var now = DateTime.UtcNow;

        return (new LocalLead
        {
            Id = $"LL-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            BusinessName = name,
            NormalizedName = NormalizeName(name),
            Category = category,
            CategoryReason = reason,
            District = district,
            Address = address,
            Phone = phone,
            NormalizedPhone = normalizedPhone,
            Website = website,
            HasWebsite = website.Length > 0,
            Rating = input.Rating,
            ReviewCount = input.ReviewCount,
            Source = source,
            SourceLabel = sourceLabel,
            Status = "New",
            Notes = (input.Notes ?? string.Empty).Trim(),
            CreatedBy = actor,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        }, null);
    }

    public static (string Category, string Reason) ChooseCategory(string businessName, string? givenCategory)
    {
        var known = LocalLeadCategoriser.MatchKnown(givenCategory);
        return known is not null
            ? (known, "Category was provided")
            : LocalLeadCategoriser.Categorise(businessName, givenCategory);
    }

    /// <summary>Lower-case letters and digits only, so "Sharma Clinic." and "sharma  clinic" compare equal.</summary>
    public static string NormalizeName(string name) =>
        new(name.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    public static string DuplicateKey(string normalizedName, string district) => $"{normalizedName}|{district}";

    /// <summary>Accepts spaces, dashes, brackets, +91 and a leading 0. Empty is allowed. Otherwise exactly 10 digits.</summary>
    public static bool TryPhone(string? raw, out string phone, out string normalized)
    {
        phone = string.Empty;
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(raw)) return true;

        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.Length == 12 && digits.StartsWith("91")) digits = digits[2..];
        else if (digits.Length == 11 && digits.StartsWith('0')) digits = digits[1..];

        if (digits.Length != 10) return false;
        normalized = digits;
        phone = raw.Trim();
        return true;
    }

    /// <summary>Empty is allowed. A missing scheme gets https://. Only http and https with a dotted host pass.</summary>
    public static bool TryWebsite(string? raw, out string website)
    {
        website = string.Empty;
        if (string.IsNullOrWhiteSpace(raw)) return true;

        var value = raw.Trim();
        if (!Regex.IsMatch(value, @"^[a-z][a-z0-9+.-]*://", RegexOptions.IgnoreCase)) value = "https://" + value;

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return false;
        if (!uri.Host.Contains('.') || uri.Host.EndsWith('.')) return false;

        website = value;
        return true;
    }

    private static (LocalLead?, string?) Fail(string message) => (null, message);
}
