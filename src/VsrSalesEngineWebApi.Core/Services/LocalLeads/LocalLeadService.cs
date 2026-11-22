using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Core.Services.LocalLeads;

public class LocalLeadService : ILocalLeadService
{
    private readonly ILocalLeadRepository _repository;
    private readonly LocalLeadImporter _importer;

    public LocalLeadService(ILocalLeadRepository repository)
    {
        _repository = repository;
        _importer = new LocalLeadImporter(repository);
    }

    public LocalLeadOptionsDto GetOptions() =>
        new(UttarPradesh.All, LocalLeadCategoriser.Categories, LocalLeadBuilder.Statuses);

    public async Task<LocalLeadPageDto> GetPagedAsync(LocalLeadFilterDto filter, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var (items, total) = await _repository.QueryAsync(filter with { Page = page, PageSize = pageSize }, cancellationToken);
        return new LocalLeadPageDto(items.Select(ToDto).ToList(), total, page, pageSize);
    }

    public async Task<LocalLeadSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _repository.GetSummaryRowsAsync(cancellationToken);

        return new LocalLeadSummaryDto(
            rows.Count,
            rows.Count(r => !r.HasWebsite),
            rows.GroupBy(r => r.District).Select(g => new DistrictCountDto(g.Key, g.Count()))
                .OrderByDescending(x => x.Count).ThenBy(x => x.District).ToList(),
            rows.GroupBy(r => r.Category).Select(g => new CategoryCountDto(g.Key, g.Count()))
                .OrderByDescending(x => x.Count).ThenBy(x => x.Category).ToList(),
            rows.GroupBy(r => (r.District, r.Category)).Select(g => new GridCellDto(g.Key.District, g.Key.Category, g.Count()))
                .OrderBy(x => x.District).ThenBy(x => x.Category).ToList());
    }

    public async Task<LocalLeadDto> CreateAsync(CreateLocalLeadDto dto, string actor, CancellationToken cancellationToken = default)
    {
        var input = new LeadInput(dto.BusinessName, dto.District, dto.Category, dto.Address, dto.Phone,
            dto.Website, dto.Rating, dto.ReviewCount, dto.Notes);

        var (lead, error) = LocalLeadBuilder.Build(input, "Manual", "Manual", actor);
        if (lead is null) throw new ArgumentException(error);

        if (await _repository.DuplicateExistsAsync(lead.NormalizedName, lead.District, lead.NormalizedPhone, null, cancellationToken))
        {
            throw new ArgumentException("A lead with the same name in this district, or the same phone number, already exists.");
        }

        return ToDto(await _repository.AddAsync(lead, cancellationToken));
    }

    public async Task<LocalLeadDto?> UpdateAsync(string id, UpdateLocalLeadDto dto, CancellationToken cancellationToken = default)
    {
        var lead = await _repository.GetByIdAsync(id, cancellationToken);
        if (lead is null) return null;

        if (dto.Status is not null)
        {
            var status = LocalLeadBuilder.Statuses.FirstOrDefault(s => string.Equals(s, dto.Status.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException($"status must be one of: {string.Join(", ", LocalLeadBuilder.Statuses)}.");
            lead.Status = status;
        }

        if (dto.Category is not null)
        {
            lead.Category = LocalLeadCategoriser.MatchKnown(dto.Category)
                ?? throw new ArgumentException($"category must be one of: {string.Join(", ", LocalLeadCategoriser.Categories)}.");
            lead.CategoryReason = "Set by a person";
        }

        if (dto.District is not null)
        {
            lead.District = UttarPradesh.Resolve(dto.District) ?? throw new ArgumentException(LocalLeadBuilder.DistrictError);
        }

        if (dto.Phone is not null)
        {
            if (!LocalLeadBuilder.TryPhone(dto.Phone, out var phone, out var normalized)) throw new ArgumentException("phone must have 10 digits");
            lead.Phone = phone;
            lead.NormalizedPhone = normalized;
        }

        if (dto.Website is not null)
        {
            if (!LocalLeadBuilder.TryWebsite(dto.Website, out var website)) throw new ArgumentException("website is not a valid URL");
            lead.Website = website;
            lead.HasWebsite = website.Length > 0;
        }

        if (dto.Notes is not null) lead.Notes = dto.Notes.Trim();

        if ((dto.District is not null || dto.Phone is not null) &&
            await _repository.DuplicateExistsAsync(lead.NormalizedName, lead.District, lead.NormalizedPhone, lead.Id, cancellationToken))
        {
            throw new ArgumentException("Another lead already has this name in this district, or this phone number.");
        }

        lead.UpdatedAtUtc = DateTime.UtcNow;
        return ToDto(await _repository.UpdateAsync(lead, cancellationToken));
    }

    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default) =>
        _repository.DeleteAsync(id, cancellationToken);

    public Task<ImportLocalLeadsResultDto> ImportAsync(ImportLocalLeadsRequestDto request, string actor, CancellationToken cancellationToken = default) =>
        _importer.ImportAsync(request, actor, cancellationToken);

    internal static LocalLeadDto ToDto(LocalLead l) => new(
        l.Id, l.BusinessName, l.Category, l.CategoryReason, l.District, l.Address, l.Phone, l.Website, l.HasWebsite,
        l.Rating, l.ReviewCount, l.Source, l.SourceLabel, l.Status, l.Notes, l.CreatedBy, l.CreatedAtUtc, l.UpdatedAtUtc, l.PlaceId, l.MapsUrl);
}
