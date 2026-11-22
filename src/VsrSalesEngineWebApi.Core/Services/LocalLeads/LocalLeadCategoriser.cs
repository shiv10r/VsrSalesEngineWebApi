using System.Text.RegularExpressions;

namespace VsrSalesEngineWebApi.Core.Services.LocalLeads;

/// <summary>
/// Rules-based categoriser: keyword lists over the business name, then over any category text that came with
/// the row. It is not AI. Every result carries a plain reason so a person can see why.
/// </summary>
public static class LocalLeadCategoriser
{
    public const string Other = "Other";

    public static readonly IReadOnlyList<string> Categories = new[]
    {
        "Healthcare", "Education & Coaching", "Restaurants & Cafes", "Retail & Shops", "Real Estate",
        "Hotels & Travel", "Manufacturing & MSME", "Professional Services", "Construction & Trades", Other
    };

    // Order matters: the first category with a match wins ("Sharma Medical Store" is Healthcare, not Retail).
    private static readonly (string Category, string[] Keywords)[] Rules =
    {
        ("Healthcare", new[] { "hospital", "clinic", "nursing home", "dental", "dentist", "diagnostic", "diagnostics",
            "pathology", "pharmacy", "medical", "chemist", "eye care", "physiotherapy", "ayurved", "ayurvedic",
            "homeopathy", "homeopathic", "doctor", "swasthya", "aushadhalay", "scan centre", "blood bank" }),
        ("Education & Coaching", new[] { "school", "vidyalaya", "vidya mandir", "mahavidyalaya", "college", "coaching",
            "classes", "academy", "institute", "tuition", "tutorial", "tutorials", "university", "playschool",
            "shiksha", "pathshala", "study centre" }),
        ("Restaurants & Cafes", new[] { "restaurant", "cafe", "café", "dhaba", "bakery", "sweets", "mithai", "biryani",
            "pizza", "kitchen", "caterer", "caterers", "catering", "juice", "chai", "bhojanalaya", "bhojnalay",
            "canteen", "food court", "tiffin", "ice cream", "burger" }),
        ("Retail & Shops", new[] { "kirana", "store", "stores", "shop", "mart", "supermarket", "general store",
            "garments", "boutique", "showroom", "electronics", "mobile", "footwear", "jewellers", "jewelers",
            "jewellery", "saree", "sarees", "cloth house", "fashion", "emporium", "bazaar", "hardware",
            "furniture", "stationers", "stationery", "optical" }),
        // Placed after Retail so "Anurag Hardware Store" stays a shop, and before Real Estate so
        // "Pragya Construction" is a construction firm.
        ("Construction & Trades", new[] { "construction", "constructions", "contractor", "contractors", "civil",
            "plumber", "plumbers", "plumbing", "electrician", "electricians", "electrical", "carpenter", "carpenters",
            "carpentry", "painter", "painters", "painting", "mason", "masonry", "tiles", "tiling", "waterproofing",
            "renovation", "roofing", "pwd" }),
        ("Real Estate", new[] { "real estate", "realty", "property", "properties", "builders", "developers", "colony",
            "housing", "plots", "township", "apartments", "estate agent" }),
        ("Hotels & Travel", new[] { "hotel", "resort", "lodge", "guest house", "homestay", "travels", "tours",
            "tourism", "taxi", "cab", "banquet", "dharamshala", "yatra", "motel" }),
        ("Manufacturing & MSME", new[] { "industries", "industry", "manufacturing", "manufacturers", "factory", "works",
            "enterprises", "mills", "udyog", "fabrication", "packaging", "plastics", "steel", "exports", "foundry" }),
        ("Professional Services", new[] { "advocate", "lawyer", "law firm", "chartered accountant", "consultancy",
            "consultants", "insurance", "architect", "architects", "interior", "interiors", "studio", "photography",
            "salon", "parlour", "parlor", "spa", "gym", "fitness", "software", "digital", "marketing", "agency",
            "tax", "finance", "loan", "services" })
    };

    private static readonly Dictionary<string, Regex> Patterns = Rules
        .SelectMany(r => r.Keywords)
        .Distinct()
        .ToDictionary(k => k, k => new Regex($@"\b{Regex.Escape(k)}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));

    /// <summary>Matches a value to one of the known category names (case-insensitive), or null.</summary>
    public static string? MatchKnown(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : Categories.FirstOrDefault(c => string.Equals(c, value.Trim(), StringComparison.OrdinalIgnoreCase));

    public static (string Category, string Reason) Categorise(string? businessName, string? categoryText)
    {
        var fromName = FirstMatch(businessName);
        if (fromName is { } n) return (n.Category, $"Matched \"{n.Keyword}\" in the name");

        var fromText = FirstMatch(categoryText);
        if (fromText is { } t) return (t.Category, $"Matched \"{t.Keyword}\" in the category text");

        return (Other, "No keyword matched the name or category text");
    }

    private static (string Category, string Keyword)? FirstMatch(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        foreach (var (category, keywords) in Rules)
        {
            // Longest keyword first so "general store" is reported rather than "store".
            foreach (var keyword in keywords.OrderByDescending(k => k.Length))
            {
                if (Patterns[keyword].IsMatch(text)) return (category, keyword);
            }
        }

        return null;
    }
}
