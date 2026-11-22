namespace VsrSalesEngineWebApi.Core.Services.AgentRelay.People;

/// <summary>How a person is connected to the post. Lower rank is listed first.</summary>
public static class PersonRelation
{
    public const string Poster = "poster";
    public const string Hiring = "hiring";
    public const string DecisionMaker = "decision-maker";
    public const string Engineering = "engineering";
    public const string Mentioned = "mentioned";

    public static int Rank(string relation) => relation switch
    {
        Poster => 0,
        Hiring => 1,
        DecisionMaker => 2,
        Engineering => 3,
        _ => 4
    };

    public static string Label(string relation) => relation switch
    {
        Poster => "the poster",
        Hiring => "a hiring contact",
        DecisionMaker => "a decision-maker",
        Engineering => "an engineering contact",
        _ => "named in the post"
    };
}

/// <param name="Relation">One of <see cref="PersonRelation"/>.</param>
/// <param name="Confidence">"high" = name and title on one line, or the poster; "medium" = adjacent lines or a name in the post text.</param>
/// <param name="Evidence">The sentence or line the person came from, when it came from text.</param>
/// <param name="Handle">The poster's username. The poster's real name is never guessed.</param>
public record RelatedPerson(
    string Name,
    string Title,
    string Relation,
    string SourceUrl,
    string Confidence,
    string? Email,
    string? Evidence = null,
    string? Handle = null);

public enum SiteOutcome
{
    NoCompanySite,
    NotChecked,
    RobotsDisallowed,
    NoPages,
    NoPeopleFound,
    Found,
    FetchFailed
}

/// <param name="People">Ranked, de-duplicated and capped at five.</param>
public record RelatedPeopleResult(
    IReadOnlyList<RelatedPerson> People,
    SiteOutcome Site,
    string? Host,
    int PagesRead,
    string? Detail = null);

/// <summary>Who we are when we fetch a page.</summary>
public static class CrawlerIdentity
{
    public const string Token = "VsrSalesEngine";
    public const string UserAgent = "VsrSalesEngine/1.0 (+contact via repository)";
}
