using System.Collections.Concurrent;
using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Infrastructure.Repositories;

public class ContactRepository : IContactRepository
{
    private static readonly ConcurrentDictionary<string, Contact> Contacts = new(StringComparer.OrdinalIgnoreCase);

    static ContactRepository()
    {
        var seed = new List<Contact>
        {
            new()
            {
                Id = "CNT-1001",
                Name = "Maya Patel",
                Company = "Northstar Digital",
                Role = "Engineering Manager",
                Email = "maya.patel@northstardigital.example",
                NormalizedEmail = "maya.patel@northstardigital.example",
                Source = "Company careers",
                SourceUrl = "https://example.com/careers/northstar",
                ChannelBasis = "ExplicitBusinessContact",
                Status = "Not contacted",
                QualificationStatus = "Verified",
                IsRoleAccount = false,
                Notes = "Direct engineering contact listed on engineering blog and team directory.",
                CreatedBy = "System",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-2),
                UpdatedAtUtc = DateTime.UtcNow.AddDays(-2)
            },
            new()
            {
                Id = "CNT-1002",
                Name = "Arjun Mehta",
                Company = "BrightPath Health",
                Role = "Talent Partner",
                Email = "arjun.mehta@brightpath.example",
                NormalizedEmail = "arjun.mehta@brightpath.example",
                Source = "Public directory",
                SourceUrl = "https://example.com/team/brightpath",
                ChannelBasis = "PublicDirectory",
                Status = "Replied",
                QualificationStatus = "Qualified",
                IsRoleAccount = false,
                Notes = "Responded confirming capacity requirements.",
                CreatedBy = "System",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-3),
                UpdatedAtUtc = DateTime.UtcNow.AddDays(-1)
            },
            new()
            {
                Id = "CNT-1003",
                Name = "Sarah Johnson",
                Company = "Vertex Logistics",
                Role = "Technology Director",
                Email = "sarah.johnson@vertexlogistics.example",
                NormalizedEmail = "sarah.johnson@vertexlogistics.example",
                Source = "Direct research",
                SourceUrl = "https://example.com/contact/vertex",
                ChannelBasis = "ExplicitBusinessContact",
                Status = "Follow-up due",
                QualificationStatus = "Verified",
                IsRoleAccount = false,
                Notes = "Follow-up scheduled after technical review.",
                CreatedBy = "System",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-4),
                UpdatedAtUtc = DateTime.UtcNow.AddDays(-1)
            },
            new()
            {
                Id = "CNT-1004",
                Name = "Daniel Kim",
                Company = "NimbusWorks",
                Role = "Procurement Lead",
                Email = "daniel.kim@nimbusworks.example",
                NormalizedEmail = "daniel.kim@nimbusworks.example",
                Source = "Public procurement",
                SourceUrl = "https://example.com/tenders/nimbus",
                ChannelBasis = "PublicDirectory",
                Status = "Do not contact",
                QualificationStatus = "OptedOut",
                IsRoleAccount = false,
                Notes = "Contact requested communication only via official tender portal.",
                CreatedBy = "System",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-5),
                UpdatedAtUtc = DateTime.UtcNow.AddDays(-2)
            }
        };

        foreach (var c in seed)
        {
            Contacts[c.Id] = c;
        }
    }

    public Task<PagedResultDto<Contact>> GetPagedAsync(ContactFilterDto filter, CancellationToken cancellationToken = default)
    {
        var query = Contacts.Values.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(filter.SearchQuery))
        {
            var q = filter.SearchQuery.Trim();
            query = query.Where(c =>
                c.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                c.Company.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                c.Email.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                c.Role.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter.Company))
        {
            query = query.Where(c => c.Company.Equals(filter.Company, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status) && !filter.Status.Equals("All statuses", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(c => c.Status.Equals(filter.Status, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter.QualificationStatus))
        {
            query = query.Where(c => c.QualificationStatus.Equals(filter.QualificationStatus, StringComparison.OrdinalIgnoreCase));
        }

        var totalCount = query.Count();
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var items = query
            .OrderByDescending(c => c.UpdatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Task.FromResult(new PagedResultDto<Contact>(items, totalCount, page, pageSize, totalPages));
    }

    public Task<IReadOnlyList<Contact>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Contact> list = Contacts.Values.OrderByDescending(c => c.UpdatedAtUtc).ToList();
        return Task.FromResult(list);
    }

    public Task<Contact?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        Contacts.TryGetValue(id, out var contact);
        return Task.FromResult(contact);
    }

    public Task<Contact?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default)
    {
        var contact = Contacts.Values.FirstOrDefault(c =>
            string.Equals(c.NormalizedEmail, normalizedEmail, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(contact);
    }

    public Task<Contact> AddAsync(Contact contact, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contact.Id))
        {
            contact.Id = $"CNT-{1000 + Contacts.Count + 1}";
        }
        Contacts[contact.Id] = contact;
        return Task.FromResult(contact);
    }

    public Task<Contact?> UpdateAsync(Contact contact, CancellationToken cancellationToken = default)
    {
        if (Contacts.ContainsKey(contact.Id))
        {
            Contacts[contact.Id] = contact;
            return Task.FromResult<Contact?>(contact);
        }
        return Task.FromResult<Contact?>(null);
    }

    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var removed = Contacts.TryRemove(id, out _);
        return Task.FromResult(removed);
    }
}
