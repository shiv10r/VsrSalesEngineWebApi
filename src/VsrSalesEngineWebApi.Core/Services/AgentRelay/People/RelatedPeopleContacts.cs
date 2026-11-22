using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Core.Services.AgentRelay.People;

/// <summary>Saves a found person as a Contact with where we found them and when. Never invents an email.</summary>
public static class RelatedPeopleContacts
{
    /// <returns>True when a new contact was created, false when the same person was already saved.</returns>
    public static async Task<bool> EnsureAsync(IContactRepository contacts, Opportunity opportunity, RelatedPerson person, CancellationToken ct)
    {
        var key = PersonNames.Key(person.Handle ?? person.Name) + "|" + PersonNames.Key(opportunity.Company);
        var existing = (await contacts.GetAllAsync(ct)).FirstOrDefault(c =>
            PersonNames.Key(c.Name) + "|" + PersonNames.Key(c.Company) == key);
        if (existing != null)
        {
            if (existing.RelationToPost == null)
            {
                existing.RelationToPost = person.Relation;
                await contacts.UpdateAsync(existing, ct);
            }
            return false;
        }

        var email = person.Email?.Trim();
        var fromSite = person.Relation is not (PersonRelation.Poster or PersonRelation.Mentioned)
            && !string.Equals(person.SourceUrl, opportunity.SourceUrl, StringComparison.OrdinalIgnoreCase);

        await contacts.AddAsync(new Contact
        {
            Id = $"CON-AG-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            Name = person.Name,
            Company = opportunity.Company,
            Role = person.Title,
            Email = email ?? string.Empty,
            NormalizedEmail = email?.ToLowerInvariant() ?? string.Empty,
            Source = fromSite ? "Company website" : opportunity.Source,
            SourceUrl = person.SourceUrl,
            ChannelBasis = email != null ? "ExplicitBusinessContact" : "ManualHandoff",
            IsRoleAccount = email != null && RoleAccountClassifier.IsRoleAccount(email),
            RelationToPost = person.Relation,
            Notes = BuildNotes(opportunity, person),
            CreatedBy = "Agent: Contact Finder"
        }, ct);

        return true;
    }

    private static string BuildNotes(Opportunity opportunity, RelatedPerson person)
    {
        var parts = new List<string>
        {
            $"{PersonRelation.Label(person.Relation)} for '{opportunity.Title}' ({person.Confidence} confidence), retrieved {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC."
        };
        if (person.Handle != null && !string.IsNullOrWhiteSpace(opportunity.PosterProfileUrl))
            parts.Add($"Username '{person.Handle}', profile {opportunity.PosterProfileUrl}. The real name is not known.");
        if (!string.IsNullOrWhiteSpace(person.Evidence) && person.Relation != PersonRelation.Poster)
            parts.Add($"Evidence: {person.Evidence}");
        return string.Join(' ', parts);
    }
}
