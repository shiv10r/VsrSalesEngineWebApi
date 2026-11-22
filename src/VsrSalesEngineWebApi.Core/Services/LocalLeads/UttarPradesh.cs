using System.Text.RegularExpressions;

namespace VsrSalesEngineWebApi.Core.Services.LocalLeads;

/// <summary>The 75 districts of Uttar Pradesh, name matching and detection of a district inside an address.</summary>
public static class UttarPradesh
{
    /// <summary>Canonical display value for Noida's district (shown to users as "Gautam Buddh Nagar (Noida)").</summary>
    public const string Noida = "Gautam Buddh Nagar (Noida)";

    private static readonly string[] Major =
    {
        Noida, "Ghaziabad", "Lucknow", "Kanpur Nagar", "Varanasi", "Prayagraj", "Agra", "Meerut",
        "Azamgarh", "Gorakhpur", "Mathura", "Bareilly", "Aligarh", "Moradabad", "Jhansi"
    };

    private static readonly string[] Others =
    {
        "Ambedkar Nagar", "Amethi", "Amroha", "Auraiya", "Ayodhya", "Baghpat", "Bahraich", "Ballia", "Balrampur",
        "Banda", "Barabanki", "Basti", "Bhadohi", "Bijnor", "Budaun", "Bulandshahr", "Chandauli", "Chitrakoot",
        "Deoria", "Etah", "Etawah", "Farrukhabad", "Fatehpur", "Firozabad", "Ghazipur", "Gonda", "Hamirpur",
        "Hapur", "Hardoi", "Hathras", "Jalaun", "Jaunpur", "Kannauj", "Kanpur Dehat", "Kasganj", "Kaushambi",
        "Kushinagar", "Lakhimpur Kheri", "Lalitpur", "Maharajganj", "Mahoba", "Mainpuri", "Mau", "Mirzapur",
        "Muzaffarnagar", "Pilibhit", "Pratapgarh", "Raebareli", "Rampur", "Saharanpur", "Sambhal",
        "Sant Kabir Nagar", "Shahjahanpur", "Shamli", "Shravasti", "Siddharthnagar", "Sitapur", "Sonbhadra",
        "Sultanpur", "Unnao"
    };

    /// <summary>Major districts first, then the rest alphabetically. 75 in total.</summary>
    public static readonly IReadOnlyList<string> All =
        Major.Concat(Others.OrderBy(d => d, StringComparer.OrdinalIgnoreCase)).ToList();

    // Alternative spellings and well-known city names -> canonical district.
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["noida"] = Noida,
        ["greater noida"] = Noida,
        ["gautam buddh nagar"] = Noida,
        ["gautam buddha nagar"] = Noida,
        ["gb nagar"] = Noida,
        ["kanpur"] = "Kanpur Nagar",
        ["allahabad"] = "Prayagraj",
        ["faizabad"] = "Ayodhya",
        ["rae bareli"] = "Raebareli",
        ["rai bareli"] = "Raebareli",
        ["bulandshahar"] = "Bulandshahr",
        ["lakhimpur"] = "Lakhimpur Kheri",
        ["kheri"] = "Lakhimpur Kheri",
        ["muzaffar nagar"] = "Muzaffarnagar",
        ["siddharth nagar"] = "Siddharthnagar",
        ["badaun"] = "Budaun",
        ["shrawasti"] = "Shravasti",
        ["kushi nagar"] = "Kushinagar",
        ["bara banki"] = "Barabanki",
        ["sant ravidas nagar"] = "Bhadohi"
    };

    private static readonly Dictionary<string, string> ByKey = BuildKeyMap();

    // Terms searched for inside an address, longest first so "Kanpur Dehat" wins over "Kanpur".
    private static readonly List<(Regex Pattern, string District)> AddressTerms = ByKey
        .OrderByDescending(kv => kv.Key.Length)
        .Select(kv => (new Regex($@"\b{Regex.Escape(kv.Key)}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), kv.Value))
        .ToList();

    private static readonly Regex OtherState = new(
        @"\b(delhi|haryana|gurgaon|gurugram|faridabad|uttarakhand|dehradun|bihar|patna|madhya pradesh|rajasthan|punjab|maharashtra|mumbai)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex UpMention = new(
        @"\buttar\s+pradesh\b|\bu\.?\s?p\.?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Matches a provided district value (any spelling or alias) to the canonical name, or null.</summary>
    public static string? Resolve(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var key = Key(value);
        key = Regex.Replace(key, @"\s*\bdistrict\b\s*", " ").Trim();
        return ByKey.TryGetValue(key, out var district) ? district : null;
    }

    /// <summary>Finds a UP district named inside an address. Null when none, or when another state is named.</summary>
    public static string? DetectInAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return null;

        foreach (var (pattern, district) in AddressTerms)
        {
            if (!pattern.IsMatch(address)) continue;

            var ncr = district is Noida or "Ghaziabad";
            if (!ncr && OtherState.IsMatch(address) && !UpMention.IsMatch(address)) return null;
            return district;
        }

        return null;
    }

    private static Dictionary<string, string> BuildKeyMap()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in All)
        {
            map[Key(name)] = name;
            var plain = Key(Regex.Replace(name, @"\s*\(.*?\)", ""));
            map[plain] = name;
        }
        foreach (var (alias, district) in Aliases) map[Key(alias)] = district;
        return map;
    }

    private static string Key(string value) =>
        Regex.Replace(Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9 ]+", " "), @"\s+", " ").Trim();
}
