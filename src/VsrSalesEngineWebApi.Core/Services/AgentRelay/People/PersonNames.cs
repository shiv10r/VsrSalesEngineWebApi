using System.Text.RegularExpressions;

namespace VsrSalesEngineWebApi.Core.Services.AgentRelay.People;

/// <summary>Decides whether a piece of text looks like a person's name. Deliberately strict: a miss is better than a wrong name.</summary>
public static class PersonNames
{
    private static readonly Regex Token = new(@"^(?:\p{Lu}\.|\p{Lu}[\p{L}'’\-]*\p{Ll}[\p{L}'’\-]*)$",
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly HashSet<string> Stop = new(StringComparer.OrdinalIgnoreCase)
    {
        "chief", "officer", "executive", "technology", "technical", "engineering", "head", "director", "manager", "vice",
        "president", "vp", "founder", "founders", "cofounder", "co", "ceo", "cto", "coo", "our", "meet", "the", "team",
        "leadership", "about", "us", "company", "and", "of", "at", "for", "with", "contact", "join", "we", "careers",
        "jobs", "hiring", "products", "product", "services", "solutions", "mission", "values", "story", "board",
        "advisors", "investors", "press", "blog", "read", "more", "view", "profile", "linkedin", "twitter", "email",
        "get", "in", "touch", "copyright", "privacy", "terms", "recruiter", "talent", "acquisition", "people", "human",
        "resources", "hr", "lead", "staff", "principal", "engineer", "software", "senior", "junior", "general",
        "partner", "partners", "office", "remote", "apply", "now", "all", "rights", "reserved", "new", "york", "san",
        "francisco", "london", "india", "bangalore", "bengaluru", "delhi", "noida", "lucknow", "inc", "llc", "ltd",
        "pvt", "limited", "labs", "systems", "group", "global", "digital", "open", "positions", "roles", "position",
        "role", "life", "culture", "benefits", "why", "work", "who", "what", "where", "when", "how", "hello", "hi",
        "dear", "thanks", "thank", "you", "your", "please", "welcome", "policy", "cookie", "cookies", "accept",
        "reject", "home", "menu", "search", "login", "sign", "register", "hacker", "news", "hn", "slack", "discord",
        "telegram", "whatsapp", "github", "skype", "zoom", "google", "form", "application", "resume", "cv", "via",
        "this", "that", "here", "there", "any", "one", "from", "see", "page", "site", "website", "internal"
    };

    public static bool IsStopWord(string word) => Stop.Contains(word.Trim().Trim('.', ',', ':', ';'));

    /// <param name="minTokens">2 for text on a page, 1 when an explicit cue such as "ask for" precedes the name.</param>
    public static bool IsName(string text, int minTokens = 2)
    {
        var s = text.Trim().Trim(',', ':', ';', '|', '-', '–', '—').Trim();
        if (s.Length < 3 || s.Length > 40) return false;

        var tokens = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < minTokens || tokens.Length > 4) return false;
        if (tokens.Any(t => !Token.IsMatch(t) || IsStopWord(t))) return false;
        return tokens.Any(t => t.Length >= 3);
    }

    /// <summary>Lower-case, single-spaced, letters only - used to compare people.</summary>
    public static string Key(string value) =>
        string.Join(' ', Regex.Replace(value.ToLowerInvariant(), @"[^\p{L}\p{N}@ ]+", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
