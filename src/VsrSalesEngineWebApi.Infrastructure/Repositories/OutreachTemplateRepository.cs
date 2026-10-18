using System.Collections.Concurrent;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Infrastructure.Repositories;

public class OutreachTemplateRepository : IOutreachTemplateRepository
{
    private static readonly ConcurrentDictionary<string, OutreachTemplate> Templates = new(StringComparer.OrdinalIgnoreCase);

    static OutreachTemplateRepository()
    {
        var seed = new List<OutreachTemplate>
        {
            new()
            {
                Id = "TPL-001",
                Name = "Cloud Architecture & Modernization Introduction",
                Category = "Cloud & Architecture",
                SubjectTemplate = "Inquiry regarding {{CompanyName}}'s {{RoleOrOpportunity}} initiative",
                BodyTemplate = "Hi {{ContactName}},\n\nI noticed {{CompanyName}} is exploring {{RoleOrOpportunity}} (via {{SourceContext}}).\n\nAt {{SenderCompany}}, we specialize in {{ServiceProposition}}.\n\nWould you be open to a brief 10-minute introductory conversation next week to explore how we can support your delivery schedule?\n\nBest regards,\n{{SenderName}}\n{{SenderTitle}}\n{{SenderCompany}}\n\n{{JurisdictionFooter}}",
                ServiceProposition = "cloud-native migration, enterprise API modernization, and scalable full-stack engineering",
                JurisdictionPolicy = "US-CANSPAM",
                FooterTemplate = "To opt out of future updates, reply 'UNSUBSCRIBE' or visit {{OptOutUrl}}.\n{{SenderCompany}}, 100 Enterprise Way, Suite 400, Austin TX 78701.",
                IsActive = true,
                CreatedBy = "System",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-7),
                UpdatedAtUtc = DateTime.UtcNow.AddDays(-7)
            },
            new()
            {
                Id = "TPL-002",
                Name = "Engineering Team Augmentation",
                Category = "Team Augmentation",
                SubjectTemplate = "{{CompanyName}}'s technical team capacity for {{RoleOrOpportunity}}",
                BodyTemplate = "Hi {{ContactName}},\n\nI saw that {{CompanyName}} is looking to expand engineering capability for {{RoleOrOpportunity}} (referenced in {{SourceContext}}).\n\n{{SenderCompany}} provides pre-vetted senior software engineers across .NET, React, and cloud infrastructure ready to integrate directly into your sprint cycles within 5 business days.\n\nCould we schedule a quick call this week to review your technical requirements?\n\nBest regards,\n{{SenderName}}\n{{SenderTitle}}\n{{SenderCompany}}\n\n{{JurisdictionFooter}}",
                ServiceProposition = "high-velocity senior team augmentation for backend .NET, cloud DevOps, and frontend React architectures",
                JurisdictionPolicy = "US-CANSPAM",
                FooterTemplate = "To opt out of future updates, reply 'UNSUBSCRIBE' or visit {{OptOutUrl}}.\n{{SenderCompany}}, 100 Enterprise Way, Suite 400, Austin TX 78701.",
                IsActive = true,
                CreatedBy = "System",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-6),
                UpdatedAtUtc = DateTime.UtcNow.AddDays(-6)
            },
            new()
            {
                Id = "TPL-003",
                Name = "Public Procurement & RFP Capability Brief",
                Category = "Procurement",
                SubjectTemplate = "Capability Statement: {{CompanyName}} - {{RoleOrOpportunity}}",
                BodyTemplate = "Dear {{ContactName}},\n\nRegarding the recent public posting for {{RoleOrOpportunity}} (source: {{SourceContext}}), {{SenderCompany}} is pleased to submit our commercial capability brief.\n\nOur engineering practice specializes in {{ServiceProposition}}, and we maintain standard compliance readiness documentation for enterprise and public vendor onboarding.\n\nPlease let us know the appropriate submission point of contact or portal to transmit our full vendor packet.\n\nSincerely,\n{{SenderName}}\n{{SenderTitle}}\n{{SenderCompany}}\n\n{{JurisdictionFooter}}",
                ServiceProposition = "enterprise software architecture, secure cloud migrations, and compliant government/commercial subcontracting",
                JurisdictionPolicy = "US-CANSPAM",
                FooterTemplate = "To opt out of future updates, reply 'UNSUBSCRIBE' or visit {{OptOutUrl}}.\n{{SenderCompany}}, 100 Enterprise Way, Suite 400, Austin TX 78701.",
                IsActive = true,
                CreatedBy = "System",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-5),
                UpdatedAtUtc = DateTime.UtcNow.AddDays(-5)
            },
            new()
            {
                Id = "TPL-004",
                Name = "EU GDPR Compliant B2B Direct Inquiry",
                Category = "Cloud & Architecture",
                SubjectTemplate = "B2B Technical Inquiry for {{CompanyName}} (re: {{RoleOrOpportunity}})",
                BodyTemplate = "Dear {{ContactName}},\n\nI am contacting you regarding {{CompanyName}}'s publicly listed opening for {{RoleOrOpportunity}}.\n\n{{SenderCompany}} assists technical organizations with {{ServiceProposition}}.\n\nIf this aligns with your current roadmap, we would be pleased to share a relevant architectural case study.\n\nKind regards,\n{{SenderName}}\n{{SenderTitle}}\n{{SenderCompany}}\n\n{{JurisdictionFooter}}",
                ServiceProposition = "cloud modernization and high-availability enterprise services",
                JurisdictionPolicy = "EU-GDPR",
                FooterTemplate = "You are receiving this B2B communication on the basis of legitimate interest related to your public professional role. To exercise your right to object or opt out at any time, reply 'UNSUBSCRIBE' or visit {{OptOutUrl}}.\nController: {{SenderCompany}}, 100 Enterprise Way, Suite 400, Austin TX 78701.",
                IsActive = true,
                CreatedBy = "System",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-4),
                UpdatedAtUtc = DateTime.UtcNow.AddDays(-4)
            }
        };

        foreach (var t in seed)
        {
            Templates[t.Id] = t;
        }
    }

    public Task<IReadOnlyList<OutreachTemplate>> GetAllAsync(string? category = null)
    {
        var query = Templates.Values.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(t => t.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        }

        var list = query.OrderByDescending(t => t.UpdatedAtUtc).ToList();
        return Task.FromResult<IReadOnlyList<OutreachTemplate>>(list);
    }

    public Task<OutreachTemplate?> GetByIdAsync(string id)
    {
        Templates.TryGetValue(id, out var template);
        return Task.FromResult(template);
    }

    public Task<OutreachTemplate> CreateAsync(OutreachTemplate template)
    {
        Templates[template.Id] = template;
        return Task.FromResult(template);
    }

    public Task<OutreachTemplate?> UpdateAsync(OutreachTemplate template)
    {
        if (!Templates.ContainsKey(template.Id))
        {
            return Task.FromResult<OutreachTemplate?>(null);
        }

        Templates[template.Id] = template;
        return Task.FromResult<OutreachTemplate?>(template);
    }

    public Task<bool> DeleteAsync(string id)
    {
        var removed = Templates.TryRemove(id, out _);
        return Task.FromResult(removed);
    }
}
