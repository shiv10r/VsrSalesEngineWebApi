using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Core.Services.AgentRelay.People;

/// <summary>The part of the Contact Finder step that looks for people connected to each post and narrates it.</summary>
public sealed class RelatedPeoplePhase
{
    private const string Agent = AgentIds.ContactFinder;

    private readonly IRelatedPeopleFinder _finder;
    private readonly IContactRepository _contacts;
    private readonly int _maxSiteLookups;

    public RelatedPeoplePhase(IRelatedPeopleFinder finder, IContactRepository contacts, int maxSiteLookups)
    {
        _finder = finder;
        _contacts = contacts;
        _maxSiteLookups = maxSiteLookups;
    }

    public async Task RunAsync(RelayState state, CancellationToken ct)
    {
        var log = state.Log;
        await log.ThoughtAsync(Agent,
            "Next I look for people connected to each post: who posted it, anyone the post names, and recruiters, " +
            "engineers or founders on the company's own team and careers pages. Company pages only - never LinkedIn, " +
            "Facebook, Instagram or YouTube - and I follow robots.txt.");

        var checkedHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var notChecked = 0;
        var people = 0;
        var companies = 0;

        foreach (var lead in state.Leads.OrderByDescending(l => l.Score))
        {
            var opportunity = lead.Opportunity;
            var site = CompanySiteLocator.FromOpportunity(opportunity);
            var allowNetwork = site != null && (checkedHosts.Contains(site.Host) || checkedHosts.Count < _maxSiteLookups);
            if (site != null && allowNetwork) checkedHosts.Add(site.Host);
            if (site != null && !allowNetwork) notChecked++;

            RelatedPeopleResult result;
            try
            {
                result = await _finder.FindAsync(opportunity, allowNetwork, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                await log.WarningAsync(Agent, $"Could not look up people for {opportunity.Company}: {KeywordText.Short(ex.Message, 100)}");
                continue;
            }

            var dto = lead.ToDto(includeScore: true);
            foreach (var person in result.People)
            {
                var created = await RelatedPeopleContacts.EnsureAsync(_contacts, opportunity, person, ct);
                await log.ResultAsync(Agent, Describe(person, opportunity, created), dto);
                people++;
            }
            if (result.People.Count > 0) companies++;

            await ReportSiteAsync(log, result);
        }

        var cap = notChecked > 0 ? $" {notChecked} lead(s) were not checked on their own site this run (limit {_maxSiteLookups} companies)." : string.Empty;
        await log.ResultAsync(Agent,
            $"Noted {people} connected person(s) across {companies} of {state.Leads.Count} lead(s). {checkedHosts.Count} company site(s) checked.{cap}");
    }

    private static async Task ReportSiteAsync(AgentLog log, RelatedPeopleResult result)
    {
        var host = result.Host;
        var fromSite = result.People.Any(p => p.Relation is PersonRelation.Hiring or PersonRelation.DecisionMaker or PersonRelation.Engineering
            && p.SourceUrl.Contains(host ?? "\u0001", StringComparison.OrdinalIgnoreCase));
        if (fromSite) return;

        switch (result.Site)
        {
            case SiteOutcome.RobotsDisallowed when result.Detail != null:
                await log.WarningAsync(Agent, $"Could not read robots.txt on {host}, so I skipped it. Nothing else was fetched.");
                break;
            case SiteOutcome.RobotsDisallowed:
                await log.WarningAsync(Agent, $"{host} asks bots not to read its team pages (robots.txt), so I skipped it. Nothing was fetched.");
                break;
            case SiteOutcome.NoPages:
                await log.ResultAsync(Agent, $"No team or careers page on {host} - leaving this one for manual research.");
                break;
            case SiteOutcome.NoPeopleFound:
                await log.ResultAsync(Agent, $"Read {result.PagesRead} page(s) on {host} but found nobody with a relevant title - leaving this one for manual research.");
                break;
            case SiteOutcome.FetchFailed:
                await log.WarningAsync(Agent, $"Could not load {host} (timeout or blocked) - skipping it.");
                break;
        }
    }

    private static string Describe(RelatedPerson person, Opportunity opportunity, bool created)
    {
        var note = created ? "Added to Contacts." : "Already in Contacts.";
        var inThePost = string.Equals(person.SourceUrl, opportunity.SourceUrl, StringComparison.OrdinalIgnoreCase);

        return person.Relation switch
        {
            PersonRelation.Poster =>
                $"{person.Handle ?? person.Name} posted this ({SourceLabel(opportunity)}) - noting them as the poster. {note}",
            PersonRelation.Mentioned =>
                $"The post names {person.Name} (\"{KeywordText.Short(person.Evidence ?? string.Empty, 120)}\") - noting them as mentioned in the post. {note}",
            _ =>
                $"Found {person.Name}, {person.Title}, {(inThePost ? "in the post" : "on " + Display(person.SourceUrl))} " +
                $"({person.Confidence} confidence) - {PersonRelation.Label(person.Relation)}. {note}"
        };
    }

    private static string SourceLabel(Opportunity opportunity) =>
        opportunity.Source.Contains("Hacker", StringComparison.OrdinalIgnoreCase) ? "Hacker News comment" : "post";

    private static string Display(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host + uri.AbsolutePath.TrimEnd('/') : url;
}
