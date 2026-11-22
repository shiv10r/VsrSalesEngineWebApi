using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Services.AgentRelay.People;
using Xunit;

namespace VsrSalesEngineWebApi.Tests;

public class PeopleExtractionTests
{
    private const string TeamPage = """
        <html><head><title>Team</title>
        <script>var cto = "Evil Person, CTO";</script><style>.x { color: red }</style></head>
        <body>
        <h1>Our team</h1>
        <div class="member"><h3><a href="mailto:priya@acme.io">Priya Sharma</a></h3><p>CTO</p></div>
        <ul><li><strong>Rahul Verma</strong> <span>Co-founder &amp; CEO</span></li></ul>
        <p>Meera Jain, Technical Recruiter</p>
        <p>Ravi Kumar - Engineering Manager</p>
        <div><div>Head of Product</div><div>Sneha Rao</div></div>
        <p>Amit Shah, Tech Lead</p>
        <p>Dev Patel</p><p>some text with no title</p>
        <p>Anita Roy is our CTO and she loves cats</p>
        <p>Chief Technology Officer</p>
        <a href="mailto:info@acme.io">info@acme.io</a>
        </body></html>
        """;

    private static IReadOnlyList<RelatedPerson> People() =>
        PagePeopleExtractor.Extract(HtmlTextLines.Extract(TeamPage), "https://acme.io/team");

    private static RelatedPerson Person(string name) => People().Single(p => p.Name == name);

    // ---- reading HTML -----------------------------------------------------------------------------------------

    [Fact]
    public void ScriptsAndStylesAreNeverReadAndBlocksStartNewLines()
    {
        var lines = HtmlTextLines.Extract(TeamPage).Select(l => l.Text).ToList();

        Assert.DoesNotContain(lines, l => l.Contains("Evil Person"));
        Assert.DoesNotContain(lines, l => l.Contains("color: red"));
        Assert.Contains("Priya Sharma", lines);               // heading and paragraph are separate lines
        Assert.Contains("CTO", lines);
        Assert.Contains("Rahul Verma Co-founder & CEO", lines); // inline tags stay on one line, entities decoded
    }

    [Fact]
    public void MailtoLinksStayWithTheLineTheyAreOn()
    {
        var lines = HtmlTextLines.Extract(TeamPage);

        Assert.Equal(new[] { "priya@acme.io" }, lines.Single(l => l.Text == "Priya Sharma").Mailtos);
        Assert.Equal(new[] { "info@acme.io" }, lines.Single(l => l.Text == "info@acme.io").Mailtos);
    }

    // ---- people on a company page --------------------------------------------------------------------------------

    [Fact]
    public void NameAndTitleOnOneLineIsHighConfidence()
    {
        var rahul = Person("Rahul Verma");
        Assert.Equal("high", rahul.Confidence);
        Assert.Equal("Co-founder & CEO", rahul.Title);
        Assert.Equal(PersonRelation.DecisionMaker, rahul.Relation);

        Assert.Equal("high", Person("Meera Jain").Confidence);
        Assert.Equal("high", Person("Ravi Kumar").Confidence);
        Assert.Equal("high", Person("Amit Shah").Confidence);
    }

    [Fact]
    public void NameAndTitleOnAdjacentLinesIsMediumConfidenceInEitherOrder()
    {
        var priya = Person("Priya Sharma");          // name, then title
        Assert.Equal("medium", priya.Confidence);
        Assert.Equal("CTO", priya.Title);

        var sneha = Person("Sneha Rao");             // title, then name
        Assert.Equal("medium", sneha.Confidence);
        Assert.Equal("Head of Product", sneha.Title);
    }

    [Fact]
    public void EachPersonIsClassifiedByTheirTitle()
    {
        Assert.Equal(PersonRelation.Hiring, Person("Meera Jain").Relation);
        Assert.Equal(PersonRelation.DecisionMaker, Person("Priya Sharma").Relation);
        Assert.Equal(PersonRelation.Engineering, Person("Ravi Kumar").Relation);     // Engineering Manager
        Assert.Equal(PersonRelation.Engineering, Person("Sneha Rao").Relation);      // Head of Product
        Assert.Equal(PersonRelation.Engineering, Person("Amit Shah").Relation);      // Tech Lead
    }

    [Fact]
    public void SentencesTitlesAloneAndUntitledNamesAreNotPeople()
    {
        var names = People().Select(p => p.Name).ToList();

        Assert.DoesNotContain("Anita Roy", names);                 // "Anita Roy is our CTO and she loves cats"
        Assert.DoesNotContain("Dev Patel", names);                 // a name with no title
        Assert.DoesNotContain(names, n => n.Contains("Chief") || n.Contains("Technology") || n.Contains("Evil"));
        Assert.Equal(6, names.Count);
    }

    [Fact]
    public void EmailsAreOnlyTakenFromTheMailtoInsideThePersonsOwnBlock()
    {
        Assert.Equal("priya@acme.io", Person("Priya Sharma").Email);
        Assert.Null(Person("Rahul Verma").Email);                   // never guessed, never built from the name
        Assert.DoesNotContain(People(), p => p.Email == "info@acme.io");
    }

    [Fact]
    public void SharedMailboxesAreNeverAttachedToAPerson()
    {
        var html = """<div><h3><a href="mailto:hr@acme.io">Meera Jain</a></h3><p>Technical Recruiter</p></div>""";

        var meera = PagePeopleExtractor.Extract(HtmlTextLines.Extract(html), "https://acme.io/careers").Single();

        Assert.Equal("Meera Jain", meera.Name);
        Assert.Null(meera.Email);
    }

    // ---- titles -----------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("Technical Recruiter", PersonRelation.Hiring)]
    [InlineData("Recruiter", PersonRelation.Hiring)]
    [InlineData("Talent Acquisition Partner", PersonRelation.Hiring)]
    [InlineData("Head of People", PersonRelation.Hiring)]
    [InlineData("HR Manager", PersonRelation.Hiring)]
    [InlineData("Hiring Manager", PersonRelation.Hiring)]
    [InlineData("Founder", PersonRelation.DecisionMaker)]
    [InlineData("Co-Founder", PersonRelation.DecisionMaker)]
    [InlineData("CEO", PersonRelation.DecisionMaker)]
    [InlineData("COO", PersonRelation.DecisionMaker)]
    [InlineData("CTO", PersonRelation.DecisionMaker)]
    [InlineData("Chief Technology Officer", PersonRelation.DecisionMaker)]
    [InlineData("Head of Engineering", PersonRelation.DecisionMaker)]
    [InlineData("VP of Engineering", PersonRelation.DecisionMaker)]
    [InlineData("Vice President Engineering", PersonRelation.DecisionMaker)]
    [InlineData("Director of Technology", PersonRelation.DecisionMaker)]
    [InlineData("Engineering Manager", PersonRelation.Engineering)]
    [InlineData("Tech Lead", PersonRelation.Engineering)]
    [InlineData("Team Lead", PersonRelation.Engineering)]
    [InlineData("Principal Engineer", PersonRelation.Engineering)]
    [InlineData("Staff Software Engineer", PersonRelation.Engineering)]
    [InlineData("Head of Product", PersonRelation.Engineering)]
    [InlineData("Product Manager", PersonRelation.Engineering)]
    public void TitlesMapToTheRightRelation(string title, string relation)
    {
        var matches = TitleClassifier.Find(title);

        Assert.Single(matches);
        Assert.Equal(relation, matches[0].Relation);
    }

    [Theory]
    [InlineData("Software Engineer")]
    [InlineData("Senior Developer")]
    [InlineData("Designer")]
    public void OrdinaryJobTitlesAreNotRelevant(string title) => Assert.Empty(TitleClassifier.Find(title));

    [Fact]
    public void ThePersonKeepsTheBestRankedRelationAcrossSeveralTitles()
    {
        var (relation, title) = TitleClassifier.Summarise(TitleClassifier.Find("Co-founder & Head of People"));

        Assert.Equal(PersonRelation.Hiring, relation);             // hiring outranks decision-maker
        Assert.Equal("Co-founder & Head of People", title);
    }

    // ---- the poster and people named in the post --------------------------------------------------------------------

    private static Opportunity HnPost(string text) => new()
    {
        Id = "OP-HN-42", Company = "Acme", Title = "Senior .NET Contract Developer",
        Source = "Hacker News (Freelance & Who's Hiring)",
        SourceUrl = "https://news.ycombinator.com/item?id=42",
        PosterHandle = "jsmith", PosterProfileUrl = "https://news.ycombinator.com/user?id=jsmith",
        PostingText = text
    };

    [Fact]
    public void ThePosterIsTheCommentAuthorsHandleNeverAGuessedRealName()
    {
        var poster = PostingPeopleExtractor.Extract(HnPost("Acme | Senior .NET Contract Developer | Remote")).Single();

        Assert.Equal(PersonRelation.Poster, poster.Relation);
        Assert.Equal("jsmith", poster.Name);
        Assert.Equal("jsmith", poster.Handle);
        Assert.Equal("high", poster.Confidence);
        Assert.Equal("https://news.ycombinator.com/item?id=42", poster.SourceUrl);   // the comment itself
        Assert.Null(poster.Email);
    }

    [Fact]
    public void PeopleNamedInThePostAreMentionedWithTheSentenceAsEvidence()
    {
        var text = "Acme | Senior .NET Contract Developer | Remote\nWe build logistics APIs. Contact Jane Roy for details or ask for Ravi.\nPlease contact Us if you like. Email me at jobs@acme.io.";

        var mentioned = PostingPeopleExtractor.Extract(HnPost(text)).Where(p => p.Relation == PersonRelation.Mentioned).ToList();

        Assert.Equal(new[] { "Jane Roy", "Ravi" }, mentioned.Select(p => p.Name).ToArray());
        Assert.All(mentioned, p =>
        {
            Assert.Equal("medium", p.Confidence);
            Assert.Equal("https://news.ycombinator.com/item?id=42", p.SourceUrl);
            Assert.Contains("Contact Jane Roy for details or ask for Ravi.", p.Evidence);
        });
    }

    [Fact]
    public void CueWordsFollowedByNonNamesAreIgnored()
    {
        var text = "Contact Us for more. Email me. Message Hacker News. Apply to HR. Reach out to The team.";

        var people = PostingPeopleExtractor.Extract(HnPost(text));

        Assert.All(people, p => Assert.Equal(PersonRelation.Poster, p.Relation));   // only the poster remains
    }

    [Fact]
    public void ANamedPersonWithATitleInThePostIsClassifiedByTheTitle()
    {
        var people = PostingPeopleExtractor.Extract(HnPost("Jane Roy, CTO, reads every application.\nTalk to Meera Jain, Technical Recruiter."));

        Assert.Equal(PersonRelation.DecisionMaker, people.Single(p => p.Name == "Jane Roy").Relation);
        Assert.Equal(PersonRelation.Hiring, people.Single(p => p.Name == "Meera Jain").Relation);
    }

    [Fact]
    public void ThePosterIsNotAlsoListedAsMentioned()
    {
        var people = PostingPeopleExtractor.Extract(HnPost("Contact Jsmith for details."));

        Assert.Single(people);
    }

    [Fact]
    public void WithoutPosterDataOrTextNobodyIsInvented()
    {
        var bare = new Opportunity { Id = "OP-1", Company = "Acme", Title = "Developer", SourceUrl = "https://acme.io/jobs/1" };

        Assert.Empty(PostingPeopleExtractor.Extract(bare));
    }
}
