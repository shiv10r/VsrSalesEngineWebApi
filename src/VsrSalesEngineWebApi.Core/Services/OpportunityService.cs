using System.Text.RegularExpressions;
using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Core.Services;

public class OpportunityService : IOpportunityService
{
    private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "New", "Needs review", "Qualified", "Contacted", "Readiness hold", "Interested", "Archived"
    };

    private static readonly HashSet<string> ValidFits = new(StringComparer.OrdinalIgnoreCase)
    {
        "High", "Medium", "Low", "Unrated"
    };

    private static readonly HashSet<string> ValidTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Individual developer", "Company/vendor", "Direct project"
    };

    private readonly IOpportunityRepository _opportunityRepository;

    public OpportunityService(IOpportunityRepository opportunityRepository)
    {
        _opportunityRepository = opportunityRepository;
    }

    public async Task<PagedResultDto<OpportunityDto>> GetPagedOpportunitiesAsync(OpportunityFilterDto filter, CancellationToken cancellationToken = default)
    {
        var pagedEntities = await _opportunityRepository.GetPagedAsync(filter, cancellationToken);
        var dtos = pagedEntities.Items.Select(MapToDto).ToList();
        return new PagedResultDto<OpportunityDto>(dtos, pagedEntities.TotalCount, pagedEntities.Page, pagedEntities.PageSize, pagedEntities.TotalPages);
    }

    public async Task<IReadOnlyList<OpportunityDto>> GetOpportunitiesAsync(OpportunityFilterDto? filter = null, CancellationToken cancellationToken = default)
    {
        var list = await _opportunityRepository.GetAllAsync(filter, cancellationToken);
        return list.Select(MapToDto).ToList();
    }

    public async Task<OpportunityDto?> GetOpportunityByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        var item = await _opportunityRepository.GetByIdAsync(id.Trim(), cancellationToken);
        return item != null ? MapToDto(item) : null;
    }

    public async Task<OpportunityDto> CreateOpportunityAsync(CreateOpportunityDto dto, string actor = "System", CancellationToken cancellationToken = default)
    {
        ValidateCreateDto(dto);

        var dedupeKey = GenerateDeduplicationKey(dto.Company, dto.Title);
        var existing = await _opportunityRepository.GetByDeduplicationKeyAsync(dedupeKey, cancellationToken);
        if (existing != null)
        {
            // Idempotent deduplication: update retrieval timestamp and return existing opportunity
            existing.RetrievedAtUtc = DateTime.UtcNow;
            existing.UpdatedAtUtc = DateTime.UtcNow;
            await _opportunityRepository.UpdateAsync(existing, cancellationToken);
            return MapToDto(existing);
        }

        var fit = string.IsNullOrWhiteSpace(dto.Fit) || !ValidFits.Contains(dto.Fit) ? "Unrated" : dto.Fit.Trim();
        var type = string.IsNullOrWhiteSpace(dto.Type) || !ValidTypes.Contains(dto.Type) ? "Individual developer" : dto.Type.Trim();

        var entity = new Opportunity
        {
            Id = $"OP-{Random.Shared.Next(1050, 9999)}",
            Company = dto.Company.Trim(),
            Title = dto.Title.Trim(),
            Source = string.IsNullOrWhiteSpace(dto.Source) ? "Manually added" : dto.Source.Trim(),
            Location = string.IsNullOrWhiteSpace(dto.Location) ? "Remote / Not specified" : dto.Location.Trim(),
            FoundAt = "Today",
            Status = "New",
            Fit = fit,
            Type = type,
            HasVendorDocRequirements = dto.HasVendorDocRequirements,
            VendorRequirementNotes = dto.VendorRequirementNotes,
            SourceUrl = dto.SourceUrl?.Trim() ?? string.Empty,
            EvidenceSnippet = dto.EvidenceSnippet?.Trim() ?? string.Empty,
            MatchedQuery = dto.MatchedQuery?.Trim() ?? string.Empty,
            DeduplicationKey = dedupeKey,
            RetrievedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            StatusHistory = new List<OpportunityStatusChange>
            {
                new()
                {
                    FromStatus = "None",
                    ToStatus = "New",
                    ChangedBy = actor,
                    Reason = "Discovered and ingested into sales pipeline",
                    ChangedAtUtc = DateTime.UtcNow
                }
            }
        };

        var created = await _opportunityRepository.AddAsync(entity, cancellationToken);
        return MapToDto(created);
    }

    public async Task<OpportunityDto?> UpdateOpportunityAsync(string id, UpdateOpportunityDto dto, string actor = "User", CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        var existing = await _opportunityRepository.GetByIdAsync(id.Trim(), cancellationToken);
        if (existing == null) return null;

        if (!string.IsNullOrWhiteSpace(dto.Company)) existing.Company = dto.Company.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Title)) existing.Title = dto.Title.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Location)) existing.Location = dto.Location.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Fit) && ValidFits.Contains(dto.Fit)) existing.Fit = dto.Fit.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Type) && ValidTypes.Contains(dto.Type)) existing.Type = dto.Type.Trim();
        if (dto.HasVendorDocRequirements.HasValue) existing.HasVendorDocRequirements = dto.HasVendorDocRequirements.Value;
        if (dto.VendorRequirementNotes != null) existing.VendorRequirementNotes = dto.VendorRequirementNotes.Trim();
        if (dto.EvidenceSnippet != null) existing.EvidenceSnippet = dto.EvidenceSnippet.Trim();

        existing.UpdatedAtUtc = DateTime.UtcNow;
        var updated = await _opportunityRepository.UpdateAsync(existing, cancellationToken);
        return updated != null ? MapToDto(updated) : null;
    }

    public async Task<OpportunityDto?> TransitionStatusAsync(string id, TransitionStatusDto dto, string actor = "User", CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(dto.NewStatus)) return null;

        var targetStatus = dto.NewStatus.Trim();
        if (!ValidStatuses.Contains(targetStatus))
        {
            throw new ArgumentException($"Invalid status '{dto.NewStatus}'. Allowed values: {string.Join(", ", ValidStatuses)}");
        }

        var existing = await _opportunityRepository.GetByIdAsync(id.Trim(), cancellationToken);
        if (existing == null) return null;

        if (string.Equals(existing.Status, targetStatus, StringComparison.OrdinalIgnoreCase))
        {
            return MapToDto(existing); // No-op if already in target status
        }

        var previousStatus = existing.Status;
        existing.Status = targetStatus;
        existing.UpdatedAtUtc = DateTime.UtcNow;

        existing.StatusHistory.Add(new OpportunityStatusChange
        {
            FromStatus = previousStatus,
            ToStatus = targetStatus,
            ChangedBy = actor,
            Reason = dto.Reason?.Trim() ?? "Status updated",
            ChangedAtUtc = DateTime.UtcNow
        });

        var updated = await _opportunityRepository.UpdateAsync(existing, cancellationToken);
        return updated != null ? MapToDto(updated) : null;
    }

    public async Task<bool> ArchiveOpportunityAsync(string id, string actor = "User", CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        var opp = await _opportunityRepository.GetByIdAsync(id.Trim(), cancellationToken);
        if (opp == null) return false;

        opp.Status = "Archived";
        opp.UpdatedAtUtc = DateTime.UtcNow;
        opp.StatusHistory.Add(new OpportunityStatusChange
        {
            FromStatus = opp.Status,
            ToStatus = "Archived",
            ChangedBy = actor,
            Reason = "Opportunity archived from pipeline",
            ChangedAtUtc = DateTime.UtcNow
        });

        await _opportunityRepository.UpdateAsync(opp, cancellationToken);
        return true;
    }

    public static string GenerateDeduplicationKey(string company, string title)
    {
        var cleanCompany = Regex.Replace(company.Trim().ToLowerInvariant(), @"\s+", " ");
        var cleanTitle = Regex.Replace(title.Trim().ToLowerInvariant(), @"\s+", " ");
        return $"{cleanCompany}|{cleanTitle}";
    }

    private static void ValidateCreateDto(CreateOpportunityDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Company))
            throw new ArgumentException("Company name is required.");
        if (string.IsNullOrWhiteSpace(dto.Title))
            throw new ArgumentException("Opportunity title is required.");
        if (dto.Company.Length > 200)
            throw new ArgumentException("Company name cannot exceed 200 characters.");
        if (dto.Title.Length > 300)
            throw new ArgumentException("Opportunity title cannot exceed 300 characters.");
    }

    private static OpportunityDto MapToDto(Opportunity o) => new(
        o.Id,
        o.Company,
        o.Title,
        o.Source,
        o.Location,
        o.FoundAt,
        o.Status,
        o.Fit,
        o.Type,
        o.HasVendorDocRequirements,
        o.VendorRequirementNotes,
        o.SourceUrl,
        o.EvidenceSnippet,
        o.RetrievedAtUtc,
        o.UpdatedAtUtc,
        o.StatusHistory.Select(h => new StatusChangeDto(
            h.FromStatus,
            h.ToStatus,
            h.ChangedBy,
            h.Reason,
            h.ChangedAtUtc
        )).ToList()
    );
}
