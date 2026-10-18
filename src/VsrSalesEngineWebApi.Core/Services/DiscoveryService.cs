using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Core.Services;

public class DiscoveryService : IDiscoveryService
{
    private readonly ISourceRegistryRepository _sourceRepository;
    private readonly ISearchProfileRepository _profileRepository;
    private readonly IOpportunityRepository _opportunityRepository;

    public DiscoveryService(
        ISourceRegistryRepository sourceRepository,
        ISearchProfileRepository profileRepository,
        IOpportunityRepository opportunityRepository)
    {
        _sourceRepository = sourceRepository;
        _profileRepository = profileRepository;
        _opportunityRepository = opportunityRepository;
    }

    public async Task<IReadOnlyList<SourceDefinitionDto>> GetSourcesAsync(CancellationToken cancellationToken = default)
    {
        var sources = await _sourceRepository.GetAllSourcesAsync(cancellationToken);
        return sources.Select(MapSource).ToList();
    }

    public async Task<SourceDefinitionDto?> UpdateSourceStatusAsync(string id, UpdateSourceStatusDto dto, CancellationToken cancellationToken = default)
    {
        var validStatuses = new[] { "Active", "Restricted", "UnderReview", "Disabled" };
        if (!validStatuses.Contains(dto.PolicyStatus, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Invalid source policy status '{dto.PolicyStatus}'. Must be one of: {string.Join(", ", validStatuses)}.");
        }

        var updated = await _sourceRepository.UpdatePolicyStatusAsync(id, dto.PolicyStatus, cancellationToken);
        return updated == null ? null : MapSource(updated);
    }

    public async Task<IReadOnlyList<SearchProfileDto>> GetProfilesAsync(CancellationToken cancellationToken = default)
    {
        var profiles = await _profileRepository.GetAllAsync(cancellationToken);
        return profiles.Select(MapProfile).ToList();
    }

    public async Task<SearchProfileDto?> GetProfileByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var profile = await _profileRepository.GetByIdAsync(id, cancellationToken);
        return profile == null ? null : MapProfile(profile);
    }

    public async Task<SearchProfileDto> CreateProfileAsync(CreateSearchProfileDto dto, string actor, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) throw new ArgumentException("Search profile name cannot be empty.");
        if (string.IsNullOrWhiteSpace(dto.TargetKeywords)) throw new ArgumentException("Target keywords cannot be empty.");

        var profile = new SearchProfile
        {
            Id = $"PRF-{DateTime.UtcNow.Ticks % 10000:D4}",
            Name = dto.Name.Trim(),
            TargetKeywords = dto.TargetKeywords.Trim(),
            ExcludedKeywords = dto.ExcludedKeywords?.Trim() ?? string.Empty,
            Location = string.IsNullOrWhiteSpace(dto.Location) ? "Remote · India" : dto.Location.Trim(),
            OpportunityType = dto.OpportunityType ?? "All relevant types",
            PublishedWithinDays = dto.PublishedWithinDays.GetValueOrDefault(7),
            SelectedSources = dto.SelectedSources?.ToList() ?? new List<string> { "Company careers", "Public job board" },
            MaxResults = dto.MaxResults.GetValueOrDefault(50),
            IsActive = true,
            CreatedBy = actor,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        var created = await _profileRepository.CreateAsync(profile, cancellationToken);
        return MapProfile(created);
    }

    public async Task<SearchProfileDto?> UpdateProfileAsync(string id, UpdateSearchProfileDto dto, string actor, CancellationToken cancellationToken = default)
    {
        var existing = await _profileRepository.GetByIdAsync(id, cancellationToken);
        if (existing == null) return null;

        if (!string.IsNullOrWhiteSpace(dto.Name)) existing.Name = dto.Name.Trim();
        if (!string.IsNullOrWhiteSpace(dto.TargetKeywords)) existing.TargetKeywords = dto.TargetKeywords.Trim();
        if (dto.ExcludedKeywords != null) existing.ExcludedKeywords = dto.ExcludedKeywords.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Location)) existing.Location = dto.Location.Trim();
        if (!string.IsNullOrWhiteSpace(dto.OpportunityType)) existing.OpportunityType = dto.OpportunityType.Trim();
        if (dto.PublishedWithinDays.HasValue) existing.PublishedWithinDays = dto.PublishedWithinDays.Value;
        if (dto.SelectedSources != null) existing.SelectedSources = dto.SelectedSources.ToList();
        if (dto.MaxResults.HasValue) existing.MaxResults = dto.MaxResults.Value;
        if (dto.IsActive.HasValue) existing.IsActive = dto.IsActive.Value;
        existing.UpdatedAtUtc = DateTime.UtcNow;

        var updated = await _profileRepository.UpdateAsync(existing, cancellationToken);
        return updated == null ? null : MapProfile(updated);
    }

    public async Task<bool> DeleteProfileAsync(string id, string actor, CancellationToken cancellationToken = default)
    {
        return await _profileRepository.DeleteAsync(id, cancellationToken);
    }

    public async Task<DiscoveryRunResultDto> RunDiscoveryAsync(RunDiscoveryRequestDto request, string actor, CancellationToken cancellationToken = default)
    {
        var runId = $"RUN-{DateTime.UtcNow.Ticks % 100000:D5}";
        var startTime = DateTime.UtcNow;

        SearchProfile? profile = null;
        if (!string.IsNullOrWhiteSpace(request.ProfileId))
        {
            profile = await _profileRepository.GetByIdAsync(request.ProfileId, cancellationToken);
        }

        var profileName = profile?.Name ?? "Manual Ad-hoc Discovery";
        var selectedSources = (request.SelectedSources != null && request.SelectedSources.Any())
            ? request.SelectedSources
            : (profile?.SelectedSources ?? new List<string> { "Company careers", "Public job board" });

        var keywords = request.TargetKeywords ?? profile?.TargetKeywords ?? ".NET Contractor";
        var location = request.Location ?? profile?.Location ?? "Remote · India";

        var allSources = await _sourceRepository.GetAllSourcesAsync(cancellationToken);
        var sourceMap = allSources.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);

        var metrics = new List<SourceMetricDto>();
        int totalEvaluated = 0;
        int totalNew = 0;
        int totalDuplicates = 0;

        foreach (var sourceName in selectedSources)
        {
            if (!sourceMap.TryGetValue(sourceName, out var sourceDef) ||
                sourceDef.PolicyStatus.Equals("Disabled", StringComparison.OrdinalIgnoreCase) ||
                sourceDef.PolicyStatus.Equals("Restricted", StringComparison.OrdinalIgnoreCase))
            {
                metrics.Add(new SourceMetricDto(
                    SourceName: sourceName,
                    RecordsFetched: 0,
                    NewMatches: 0,
                    RejectedByPolicy: 1,
                    Status: "PolicyRestricted"
                ));
                continue;
            }

            // Simulate deterministic discovery evaluation per source
            int fetched = 4;
            int newMatches = 0;
            int dups = 0;

            await _sourceRepository.IncrementDailyRequestCountAsync(sourceDef.Id, 1, cancellationToken);

            var simulatedCandidates = GenerateSimulatedCandidates(sourceName, keywords, location);
            foreach (var candidate in simulatedCandidates)
            {
                totalEvaluated++;
                var existing = await _opportunityRepository.GetByDeduplicationKeyAsync(candidate.DeduplicationKey, cancellationToken);
                if (existing != null)
                {
                    dups++;
                    totalDuplicates++;
                }
                else
                {
                    await _opportunityRepository.AddAsync(candidate, cancellationToken);
                    newMatches++;
                    totalNew++;
                }
            }

            metrics.Add(new SourceMetricDto(
                SourceName: sourceName,
                RecordsFetched: fetched,
                NewMatches: newMatches,
                RejectedByPolicy: 0,
                Status: "Success"
            ));
        }

        return new DiscoveryRunResultDto(
            RunId: runId,
            InitiatedBy: actor,
            ProfileName: profileName,
            InitiatedAtUtc: startTime,
            CompletedAtUtc: DateTime.UtcNow,
            Status: "Completed",
            EvaluatedCount: totalEvaluated,
            NewMatchesCount: totalNew,
            DuplicateCount: totalDuplicates,
            SourceMetrics: metrics
        );
    }

    private static List<Opportunity> GenerateSimulatedCandidates(string sourceName, string keywords, string location)
    {
        var list = new List<Opportunity>();
        var timestamp = DateTime.UtcNow;

        if (sourceName.Contains("careers", StringComparison.OrdinalIgnoreCase))
        {
            list.Add(new Opportunity
            {
                Id = $"OP-{timestamp.Ticks % 9000 + 2000}",
                Company = "Apex Global Solutions",
                Title = $"{keywords.Split(',')[0].Trim()} Lead",
                Source = sourceName,
                Location = location,
                FoundAt = "Discovered Just Now",
                Status = "New",
                Fit = "High",
                Type = "Individual developer",
                HasVendorDocRequirements = false,
                SourceUrl = "https://careers.apexsolutions.example/postings/lead",
                EvidenceSnippet = $"Direct match for {keywords} on company career site.",
                DeduplicationKey = "apex global solutions|lead",
                RetrievedAtUtc = timestamp,
                UpdatedAtUtc = timestamp
            });
        }
        else if (sourceName.Contains("procurement", StringComparison.OrdinalIgnoreCase))
        {
            list.Add(new Opportunity
            {
                Id = $"OP-{timestamp.Ticks % 9000 + 3000}",
                Company = "National Tech Authority",
                Title = "Enterprise Cloud Integration RFP",
                Source = sourceName,
                Location = location,
                FoundAt = "Discovered Just Now",
                Status = "Readiness hold",
                Fit = "Medium",
                Type = "Company/vendor",
                HasVendorDocRequirements = true,
                VendorRequirementNotes = "Mandatory vendor registration with GSTIN and ISO certification.",
                SourceUrl = "https://eprocure.gov.example/tenders/4092",
                EvidenceSnippet = "RFP requirement for software modernize vendor partner.",
                DeduplicationKey = "national tech authority|enterprise cloud integration rfp",
                RetrievedAtUtc = timestamp,
                UpdatedAtUtc = timestamp
            });
        }

        return list;
    }

    private static SourceDefinitionDto MapSource(SourceDefinition s) => new(
        s.Id,
        s.Name,
        s.Description,
        s.Category,
        s.PolicyStatus,
        s.AllowsDirectFetch,
        s.TermsUrl,
        s.AttributionNotice,
        s.DailyRequestLimit,
        s.CurrentDayRequests,
        s.HealthStatus,
        s.LastCheckedAtUtc
    );

    private static SearchProfileDto MapProfile(SearchProfile p) => new(
        p.Id,
        p.Name,
        p.TargetKeywords,
        p.ExcludedKeywords,
        p.Location,
        p.OpportunityType,
        p.PublishedWithinDays,
        p.SelectedSources,
        p.MaxResults,
        p.IsActive,
        p.CreatedBy,
        p.CreatedAtUtc,
        p.UpdatedAtUtc
    );
}
