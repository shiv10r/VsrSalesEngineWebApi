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
                Status = "Verified",
                QualificationStatus = "Verified",
                IsRoleAccount = false,
                Notes = "Direct engineering contact listed on engineering blog and team directory.",
                CreatedBy = "System",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-2),
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
