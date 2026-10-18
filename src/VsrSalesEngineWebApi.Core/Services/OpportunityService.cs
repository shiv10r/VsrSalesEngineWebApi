using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Core.Services;

public class OpportunityService : IOpportunityService
{
    private readonly IOpportunityRepository _opportunityRepository;

    public OpportunityService(IOpportunityRepository opportunityRepository)
    {
        _opportunityRepository = opportunityRepository;
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

    public async Task<OpportunityDto> CreateOpportunityAsync(CreateOpportunityDto dto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Company))
            throw new ArgumentException("Company name is required.");
        if (string.IsNullOrWhiteSpace(dto.Title))
            throw new ArgumentException("Opportunity title is required.");

        var dedupeKey = $"{dto.Company.Trim().ToLowerInvariant()}|{dto.Title.Trim().ToLowerInvariant()}";
        var existing = await _opportunityRepository.GetByDeduplicationKeyAsync(dedupeKey, cancellationToken);
        if (existing != null)
        {
            // Return existing deduplicated match
            return MapToDto(existing);
        }

        var entity = new Opportunity
        {
            Id = $"OP-{Random.Shared.Next(1050, 9999)}",
            Company = dto.Company.Trim(),
            Title = dto.Title.Trim(),
            Source = string.IsNullOrWhiteSpace(dto.Source) ? "Manually added" : dto.Source.Trim(),
            Location = string.IsNullOrWhiteSpace(dto.Location) ? "Remote / Not specified" : dto.Location.Trim(),
            FoundAt = "Just now",
            Status = "New",
            Fit = string.IsNullOrWhiteSpace(dto.Fit) ? "Unrated" : dto.Fit.Trim(),
            Type = string.IsNullOrWhiteSpace(dto.Type) ? "Direct project" : dto.Type.Trim(),
            HasVendorDocRequirements = dto.HasVendorDocRequirements,
            SourceUrl = dto.SourceUrl ?? string.Empty,
            EvidenceSnippet = dto.EvidenceSnippet ?? string.Empty,
            DeduplicationKey = dedupeKey,
            RetrievedAtUtc = DateTime.UtcNow
        };

        var created = await _opportunityRepository.AddAsync(entity, cancellationToken);
        return MapToDto(created);
    }

    public async Task<OpportunityDto?> UpdateStatusAsync(string id, string newStatus, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(newStatus)) return null;
        var updated = await _opportunityRepository.UpdateStatusAsync(id.Trim(), newStatus.Trim(), cancellationToken);
        return updated != null ? MapToDto(updated) : null;
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
        o.SourceUrl,
        o.EvidenceSnippet
    );
}

