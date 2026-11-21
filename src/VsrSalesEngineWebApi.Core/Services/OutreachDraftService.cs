using System.Text.RegularExpressions;
using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Core.Services.Drafting;

namespace VsrSalesEngineWebApi.Core.Services;

public class OutreachDraftService : IOutreachDraftService
{
    private readonly IOutreachTemplateRepository _templateRepository;

    private const string DefaultServiceProposition = "cloud-native migration, enterprise API modernization, and scalable full-stack engineering";
    private const string DefaultSenderName = "VSR Outreach Lead";
    private const string DefaultSenderTitle = "Senior Client Partner";
    private const string DefaultSenderCompany = "VSR Technologies";
    private const string DefaultOptOutUrl = "https://vsrtechnologies.com/outreach/opt-out";
    private const string DefaultPhysicalAddress = "100 Enterprise Way, Suite 400, Austin TX 78701";

    private readonly ILLMRouter? _llmRouter;

    public OutreachDraftService(IOutreachTemplateRepository templateRepository, ILLMRouter? llmRouter = null)
    {
        _templateRepository = templateRepository;
        _llmRouter = llmRouter;
    }

    public async Task<IReadOnlyList<OutreachTemplateDto>> GetTemplatesAsync(string? category = null)
    {
        var templates = await _templateRepository.GetAllAsync(category);
        return templates.Select(ToDto).ToList();
    }

    public async Task<OutreachTemplateDto?> GetTemplateByIdAsync(string id)
    {
        var template = await _templateRepository.GetByIdAsync(id);
        return template == null ? null : ToDto(template);
    }

    public async Task<OutreachTemplateDto> CreateTemplateAsync(CreateOutreachTemplateDto dto, string user)
    {
        var entity = new OutreachTemplate
        {
            Id = $"TPL-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            Name = dto.Name.Trim(),
            Category = string.IsNullOrWhiteSpace(dto.Category) ? "General" : dto.Category.Trim(),
            SubjectTemplate = dto.SubjectTemplate.Trim(),
            BodyTemplate = dto.BodyTemplate.Trim(),
            ServiceProposition = string.IsNullOrWhiteSpace(dto.ServiceProposition) ? DefaultServiceProposition : dto.ServiceProposition.Trim(),
            JurisdictionPolicy = string.IsNullOrWhiteSpace(dto.JurisdictionPolicy) ? "US-CANSPAM" : dto.JurisdictionPolicy.Trim(),
            FooterTemplate = string.IsNullOrWhiteSpace(dto.FooterTemplate) ? DraftText.DefaultFooter(dto.JurisdictionPolicy ?? "US-CANSPAM") : dto.FooterTemplate.Trim(),
            IsActive = true,
            CreatedBy = user,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        var created = await _templateRepository.CreateAsync(entity);
        return ToDto(created);
    }

    public async Task<OutreachTemplateDto?> UpdateTemplateAsync(string id, UpdateOutreachTemplateDto dto)
    {
        var existing = await _templateRepository.GetByIdAsync(id);
        if (existing == null) return null;

        existing.Name = dto.Name.Trim();
        existing.Category = dto.Category.Trim();
        existing.SubjectTemplate = dto.SubjectTemplate.Trim();
        existing.BodyTemplate = dto.BodyTemplate.Trim();
        if (!string.IsNullOrWhiteSpace(dto.ServiceProposition)) existing.ServiceProposition = dto.ServiceProposition.Trim();
        if (!string.IsNullOrWhiteSpace(dto.JurisdictionPolicy)) existing.JurisdictionPolicy = dto.JurisdictionPolicy.Trim();
        if (!string.IsNullOrWhiteSpace(dto.FooterTemplate)) existing.FooterTemplate = dto.FooterTemplate.Trim();
        if (dto.IsActive.HasValue) existing.IsActive = dto.IsActive.Value;
        existing.UpdatedAtUtc = DateTime.UtcNow;

        var updated = await _templateRepository.UpdateAsync(existing);
        return updated == null ? null : ToDto(updated);
    }

    public async Task<bool> DeleteAsync(string id)
    {
        return await _templateRepository.DeleteAsync(id);
    }

    public Task<bool> DeleteTemplateAsync(string id)
    {
        return _templateRepository.DeleteAsync(id);
    }

    public async Task<DraftGenerationResult> GenerateDraftAsync(DraftGenerationRequest request)
    {
        OutreachTemplate? template = null;
        if (!string.IsNullOrWhiteSpace(request.TemplateId))
        {
            template = await _templateRepository.GetByIdAsync(request.TemplateId);
        }

        if (template == null)
        {
            var all = await _templateRepository.GetAllAsync();
            template = all.FirstOrDefault(t => t.IsActive) ?? GetDefaultTemplate();
        }

        var jurisdiction = string.IsNullOrWhiteSpace(request.JurisdictionPolicy) ? template.JurisdictionPolicy : request.JurisdictionPolicy;
        var serviceProposition = string.IsNullOrWhiteSpace(request.ServiceProposition) ? template.ServiceProposition : request.ServiceProposition;
        var senderName = string.IsNullOrWhiteSpace(request.SenderName) ? DefaultSenderName : request.SenderName;
        var senderTitle = string.IsNullOrWhiteSpace(request.SenderTitle) ? DefaultSenderTitle : request.SenderTitle;
        var senderCompany = string.IsNullOrWhiteSpace(request.SenderCompany) ? DefaultSenderCompany : request.SenderCompany;

        var footer = DraftText.DefaultFooter(jurisdiction)
            .Replace("{{OptOutUrl}}", DefaultOptOutUrl)
            .Replace("{{SenderCompany}}", senderCompany);

        var factsUsed = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.ContactName)) factsUsed.Add($"Contact: {request.ContactName.Trim()}");
        if (!string.IsNullOrWhiteSpace(request.CompanyName)) factsUsed.Add($"Company: {request.CompanyName.Trim()}");
        if (!string.IsNullOrWhiteSpace(request.Role)) factsUsed.Add($"Role: {request.Role.Trim()}");
        if (!string.IsNullOrWhiteSpace(request.OpportunityTitle)) factsUsed.Add($"Opportunity: {request.OpportunityTitle.Trim()}");
        if (!string.IsNullOrWhiteSpace(request.SourceEvidenceSnippet)) factsUsed.Add($"Source evidence: {request.SourceEvidenceSnippet.Trim()}");

        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "ContactName", request.ContactName?.Trim() ?? string.Empty },
            { "CompanyName", request.CompanyName?.Trim() ?? string.Empty },
            { "Role", request.Role?.Trim() ?? "team member" },
            { "OpportunityTitle", request.OpportunityTitle?.Trim() ?? "modernization project" },
            { "RoleOrOpportunity", !string.IsNullOrWhiteSpace(request.OpportunityTitle) ? request.OpportunityTitle.Trim() : (!string.IsNullOrWhiteSpace(request.Role) ? request.Role.Trim() : "technology initiatives") },
            { "SourceContext", DraftText.SanitizeSourceSnippet(request.SourceEvidenceSnippet) },
            { "ServiceProposition", serviceProposition },
            { "SenderName", senderName },
            { "SenderTitle", senderTitle },
            { "SenderCompany", senderCompany },
            { "JurisdictionFooter", footer },
            { "OptOutUrl", DefaultOptOutUrl }
        };

        var subject = ReplaceVariables(template.SubjectTemplate, variables);
        var body = ReplaceVariables(template.BodyTemplate, variables);

        if (!body.Contains(footer, StringComparison.OrdinalIgnoreCase))
        {
            body = body.TrimEnd() + "\n\n" + footer;
        }

        var validation = ValidateDraft(new DraftContentValidationRequest(subject, body, jurisdiction));

        return new DraftGenerationResult(
            Subject: subject,
            Body: body,
            FactsUsed: factsUsed,
            IsValid: validation.IsValid,
            MissingVariables: validation.MissingVariables,
            ProhibitedClaims: validation.ProhibitedClaims,
            Warnings: validation.Warnings,
            Tone: "Professional B2B Consultative",
            JurisdictionFooter: footer
        );
    }

    public DraftValidationResult ValidateDraft(DraftContentValidationRequest request)
        => DraftValidator.Validate(request);

    private static string ReplaceVariables(string text, IReadOnlyDictionary<string, string> variables)
    {
        var result = text;
        foreach (var (key, value) in variables)
        {
            result = result.Replace("{{" + key + "}}", value, StringComparison.OrdinalIgnoreCase);
        }
        return result;
    }

    private static OutreachTemplate GetDefaultTemplate()
    {
        return new OutreachTemplate
        {
            Id = "TPL-DEFAULT",
            Name = "Enterprise Cloud & Architecture Inquiry",
            Category = "Cloud & Architecture",
            SubjectTemplate = "Inquiry regarding {{CompanyName}}'s {{RoleOrOpportunity}} initiative",
            BodyTemplate = "Hi {{ContactName}},\n\nI noticed {{CompanyName}} is exploring {{RoleOrOpportunity}} (via {{SourceContext}}).\n\nAt {{SenderCompany}}, we specialize in {{ServiceProposition}}.\n\nWould you be open to a 10-minute introductory conversation next week?\n\nBest regards,\n{{SenderName}}\n{{SenderTitle}}\n{{SenderCompany}}\n\n{{JurisdictionFooter}}",
            ServiceProposition = DefaultServiceProposition,
            JurisdictionPolicy = "US-CANSPAM",
            FooterTemplate = DraftText.DefaultFooter("US-CANSPAM"),
            IsActive = true,
            CreatedBy = "System"
        };
    }

    private static OutreachTemplateDto ToDto(OutreachTemplate t) => new(
        t.Id,
        t.Name,
        t.Category,
        t.SubjectTemplate,
        t.BodyTemplate,
        t.ServiceProposition,
        t.JurisdictionPolicy,
        t.FooterTemplate,
        t.IsActive,
        t.CreatedBy,
        t.CreatedAtUtc,
        t.UpdatedAtUtc
    );
}
