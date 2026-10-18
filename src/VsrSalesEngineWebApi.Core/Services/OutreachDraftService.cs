using System.Text.RegularExpressions;
using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Core.Services;

public class OutreachDraftService : IOutreachDraftService
{
    private readonly IOutreachTemplateRepository _templateRepository;

    private static readonly string[] ProhibitedClaimsList = new[]
    {
        "100% guarantee",
        "guarantee 100%",
        "guaranteed results",
        "exclusive partner",
        "exclusive partnership",
        "certified government contractor",
        "official partner of your company",
        "we built your current system",
        "we built your existing",
        "we created your platform",
        "proprietary access to your data",
        "internal security backdoor",
        "zero risk",
        "no risk"
    };

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
            FooterTemplate = string.IsNullOrWhiteSpace(dto.FooterTemplate) ? GetDefaultFooter(dto.JurisdictionPolicy ?? "US-CANSPAM") : dto.FooterTemplate.Trim(),
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

        var footer = GetDefaultFooter(jurisdiction)
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
            { "SourceContext", SanitizeSourceSnippet(request.SourceEvidenceSnippet) },
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
    {
        var missingVars = new List<string>();
        var prohibitedClaims = new List<string>();
        var warnings = new List<string>();

        // 1. Check for missing/unsubstituted template tags {{Variable}}
        var regex = new Regex(@"\{\{([A-Za-z0-9_]+)\}\}", RegexOptions.Compiled);
        foreach (Match match in regex.Matches(request.Subject + " " + request.Body))
        {
            var varName = match.Groups[1].Value;
            if (!missingVars.Contains(varName))
            {
                missingVars.Add(varName);
            }
        }

        // 2. Check for prohibited / fabricated claims
        var fullText = (request.Subject + " " + request.Body).ToLowerInvariant();
        foreach (var claim in ProhibitedClaimsList)
        {
            if (fullText.Contains(claim.ToLowerInvariant()))
            {
                prohibitedClaims.Add($"Contains prohibited claim: '{claim}'");
            }
        }

        // 3. Excessive repetition check
        var words = Regex.Split(fullText, @"\W+").Where(w => w.Length > 4).ToList();
        var counts = words.GroupBy(w => w).Where(g => g.Count() >= 5).Select(g => g.Key).ToList();
        if (counts.Any())
        {
            warnings.Add($"Excessive word repetition detected for: {string.Join(", ", counts)}");
        }

        // 4. Compliance footer checks
        var hasUnsub = fullText.Contains("unsubscribe") || fullText.Contains("opt-out") || fullText.Contains("opt out") || fullText.Contains("stop");
        var hasAddress = fullText.Contains("100 enterprise way") || fullText.Contains("suite") || fullText.Contains("street") || fullText.Contains("austin");
        var hasSenderIdentity = fullText.Contains("vsr technologies") || fullText.Contains("vsr");

        if (!hasUnsub)
        {
            warnings.Add("Missing required opt-out/unsubscribe instructions in footer.");
        }
        if (!hasAddress)
        {
            warnings.Add("Missing required physical business mailing address in footer.");
        }
        if (!hasSenderIdentity)
        {
            warnings.Add("Missing clear sender commercial identity.");
        }

        var isValid = missingVars.Count == 0 && prohibitedClaims.Count == 0 && hasUnsub && hasAddress;

        return new DraftValidationResult(
            IsValid: isValid,
            MissingVariables: missingVars,
            ProhibitedClaims: prohibitedClaims,
            Warnings: warnings,
            HasUnsubscribeMechanism: hasUnsub,
            HasPhysicalAddress: hasAddress,
            HasSenderIdentity: hasSenderIdentity
        );
    }

    private static string ReplaceVariables(string text, IReadOnlyDictionary<string, string> variables)
    {
        var result = text;
        foreach (var (key, value) in variables)
        {
            result = result.Replace("{{" + key + "}}", value, StringComparison.OrdinalIgnoreCase);
        }
        return result;
    }

    private static string GetDefaultFooter(string jurisdiction)
    {
        return jurisdiction.ToUpperInvariant() switch
        {
            "EU-GDPR" =>
                "You are receiving this B2B communication on the basis of legitimate interest related to your public professional role. To exercise your right to object or opt out at any time, reply 'UNSUBSCRIBE' or visit {{OptOutUrl}}.\nController: {{SenderCompany}}, 100 Enterprise Way, Suite 400, Austin TX 78701.",
            "UK-PECR" =>
                "This message is intended for corporate subscribers under UK PECR regulation. If you wish to opt out of further business communications, reply 'STOP' or visit {{OptOutUrl}}.\n{{SenderCompany}}, 100 Enterprise Way, Suite 400, Austin TX 78701.",
            _ =>
                "To opt out of future updates, reply 'UNSUBSCRIBE' or visit {{OptOutUrl}}.\n{{SenderCompany}}, 100 Enterprise Way, Suite 400, Austin TX 78701."
        };
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
            FooterTemplate = GetDefaultFooter("US-CANSPAM"),
            IsActive = true,
            CreatedBy = "System"
        };
    }

    private static string SanitizeSourceSnippet(string? snippet)
    {
        if (string.IsNullOrWhiteSpace(snippet)) return "your public technical requirements";
        var sanitized = snippet.Trim();
        string[] injectionPatterns = {
            "ignore all previous instructions",
            "ignore previous instructions",
            "output the api",
            "system prompt",
            "output secret"
        };

        foreach (var pattern in injectionPatterns)
        {
            if (sanitized.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return "your public technical requirements";
            }
        }

        return sanitized;
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
