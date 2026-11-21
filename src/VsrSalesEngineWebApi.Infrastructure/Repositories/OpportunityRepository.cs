using Microsoft.EntityFrameworkCore;
using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Infrastructure.Data;

namespace VsrSalesEngineWebApi.Infrastructure.Repositories;

public class OpportunityRepository : IOpportunityRepository
{
    private readonly VsrDbContext _context;

    public OpportunityRepository(VsrDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResultDto<Opportunity>> GetPagedAsync(OpportunityFilterDto filter, CancellationToken cancellationToken = default)
    {
        var filtered = await FilterOpportunitiesAsync(filter, cancellationToken);
        var total = filtered.Count;
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var totalPages = (int)Math.Ceiling(total / (double)pageSize);

        var paged = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new PagedResultDto<Opportunity>(paged, total, page, pageSize, totalPages);
    }

    public async Task<IReadOnlyList<Opportunity>> GetAllAsync(OpportunityFilterDto? filter = null, CancellationToken cancellationToken = default)
    {
        return await FilterOpportunitiesAsync(filter ?? new OpportunityFilterDto(null, null, null, null, null), cancellationToken);
    }

    public async Task<Opportunity?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _context.Opportunities.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<Opportunity?> GetByDeduplicationKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        var all = await _context.Opportunities.AsNoTracking().ToListAsync(cancellationToken);
        return all.FirstOrDefault(o => string.Equals(o.DeduplicationKey, key, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<Opportunity> AddAsync(Opportunity opportunity, CancellationToken cancellationToken = default)
    {
        _context.Opportunities.Add(opportunity);
        await _context.SaveChangesAsync(cancellationToken);
        return opportunity;
    }

    public async Task<Opportunity?> UpdateAsync(Opportunity opportunity, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Opportunities.FirstOrDefaultAsync(o => o.Id == opportunity.Id, cancellationToken);
        if (existing == null) return null;

        _context.Entry(existing).CurrentValues.SetValues(opportunity);
        existing.StatusHistory = opportunity.StatusHistory;
        await _context.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Opportunities.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (existing == null) return false;

        _context.Opportunities.Remove(existing);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<List<Opportunity>> FilterOpportunitiesAsync(OpportunityFilterDto filter, CancellationToken cancellationToken)
    {
        var all = await _context.Opportunities.AsNoTracking().ToListAsync(cancellationToken);
        IEnumerable<Opportunity> query = all;

        if (!string.IsNullOrWhiteSpace(filter.SearchQuery))
        {
            var term = filter.SearchQuery.Trim().ToLowerInvariant();
            query = query.Where(o =>
                o.Company.ToLowerInvariant().Contains(term) ||
                o.Title.ToLowerInvariant().Contains(term) ||
                o.Source.ToLowerInvariant().Contains(term) ||
                o.Location.ToLowerInvariant().Contains(term) ||
                o.Id.ToLowerInvariant().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status) && !string.Equals(filter.Status, "All statuses", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(o => string.Equals(o.Status, filter.Status, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter.Source) && !string.Equals(filter.Source, "All sources", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(o => string.Equals(o.Source, filter.Source, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter.Fit) && !string.Equals(filter.Fit, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(o => string.Equals(o.Fit, filter.Fit, StringComparison.OrdinalIgnoreCase));
        }

        if (filter.HasVendorDocs.HasValue)
        {
            query = query.Where(o => o.HasVendorDocRequirements == filter.HasVendorDocs.Value);
        }

        return query.OrderByDescending(o => o.RetrievedAtUtc).ToList();
    }
}
