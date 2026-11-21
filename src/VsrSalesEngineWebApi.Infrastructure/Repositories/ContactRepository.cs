using Microsoft.EntityFrameworkCore;
using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Infrastructure.Data;

namespace VsrSalesEngineWebApi.Infrastructure.Repositories;

public class ContactRepository : IContactRepository
{
    private readonly VsrDbContext _context;

    public ContactRepository(VsrDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResultDto<Contact>> GetPagedAsync(ContactFilterDto filter, CancellationToken cancellationToken = default)
    {
        var all = await _context.Contacts.AsNoTracking().ToListAsync(cancellationToken);
        var query = all.AsEnumerable();

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

        return new PagedResultDto<Contact>(items, totalCount, page, pageSize, totalPages);
    }

    public async Task<IReadOnlyList<Contact>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Contacts.AsNoTracking().OrderByDescending(c => c.UpdatedAtUtc).ToListAsync(cancellationToken);
    }

    public async Task<Contact?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _context.Contacts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<Contact?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default)
    {
        var all = await _context.Contacts.AsNoTracking().ToListAsync(cancellationToken);
        return all.FirstOrDefault(c => string.Equals(c.NormalizedEmail, normalizedEmail, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<Contact> AddAsync(Contact contact, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contact.Id))
        {
            var count = await _context.Contacts.CountAsync(cancellationToken);
            contact.Id = $"CNT-{1000 + count + 1}";
        }
        _context.Contacts.Add(contact);
        await _context.SaveChangesAsync(cancellationToken);
        return contact;
    }

    public async Task<Contact?> UpdateAsync(Contact contact, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Contacts.FirstOrDefaultAsync(c => c.Id == contact.Id, cancellationToken);
        if (existing == null) return null;

        _context.Entry(existing).CurrentValues.SetValues(contact);
        await _context.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Contacts.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (existing == null) return false;

        _context.Contacts.Remove(existing);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
