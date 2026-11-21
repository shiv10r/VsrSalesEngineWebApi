using VsrSalesEngineWebApi.Core.Entities;

namespace VsrSalesEngineWebApi.Infrastructure.Data;

public static class VsrDbSeeder
{
    public static async Task SeedAsync(VsrDbContext context, CancellationToken cancellationToken = default)
    {
        if (!await Any(context.Opportunities, cancellationToken))
        {
            context.Opportunities.Add(new Opportunity
            {
                Id = "OP-1001",
                Company = "Northstar Digital",
                Title = "Senior .NET / React Engineer",
                Source = "Company careers",
                Location = "Remote · India",
                FoundAt = "Today, 10:42 AM",
                Status = "Qualified",
                Fit = "High",
                Type = "Direct project",
                HasVendorDocRequirements = true,
                VendorRequirementNotes = "Verified supplier onboarding registration and technical requirements.",
                SourceUrl = "https://example.com/careers/northstar-dotnet",
                EvidenceSnippet = "Looking for a Senior .NET Core / React engineer with 5+ years building scalable B2B APIs.",
                DeduplicationKey = "northstar digital|senior .net / react engineer",
                RetrievedAtUtc = DateTime.UtcNow.AddMinutes(-45),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-45),
                StatusHistory = new List<OpportunityStatusChange>
                {
                    new() { FromStatus = "New", ToStatus = "Qualified", ChangedBy = "VSR Admin", Reason = "Technical requirements and budget verified", ChangedAtUtc = DateTime.UtcNow.AddMinutes(-45) }
                }
            });
        }

        if (!await Any(context.Contacts, cancellationToken))
        {
            context.Contacts.Add(new Contact
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
            });
        }

        if (!await Any(context.SearchProfiles, cancellationToken))
        {
            context.SearchProfiles.AddRange(
                new SearchProfile
                {
                    Id = "PRF-001",
                    Name = ".NET & Cloud Modernization Leads",
                    TargetKeywords = ".NET Core, C#, Azure, Microservices, API Modernization",
                    ExcludedKeywords = "internship, unpaid, wordpress",
                    Location = "India · Remote",
                    OpportunityType = "All relevant types",
                    PublishedWithinDays = 7,
                    SelectedSources = new List<string> { "Company careers", "Public job board", "Approved feed" },
                    MaxResults = 50,
                    IsActive = true,
                    CreatedBy = "System",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-5),
                    UpdatedAtUtc = DateTime.UtcNow.AddDays(-1)
                },
                new SearchProfile
                {
                    Id = "PRF-002",
                    Name = "React & Full-Stack Frontend Contracts",
                    TargetKeywords = "React, TypeScript, Next.js, Frontend Architecture",
                    ExcludedKeywords = "junior, student",
                    Location = "Remote · Global",
                    OpportunityType = "Individual developer",
                    PublishedWithinDays = 14,
                    SelectedSources = new List<string> { "Company careers", "Public job board" },
                    MaxResults = 40,
                    IsActive = true,
                    CreatedBy = "System",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-3),
                    UpdatedAtUtc = DateTime.UtcNow.AddHours(-12)
                });
        }

        if (!await Any(context.Sources, cancellationToken))
        {
            context.Sources.AddRange(
                new SourceDefinition { Id = "SRC-001", Name = "Company careers", Description = "Target corporate career pages and approved public job boards.", Category = "Careers", PolicyStatus = "Active", AllowsDirectFetch = true, TermsUrl = "https://vsrsystems.com/compliance/source-policy", AttributionNotice = "Extracted from public career portal with source link retained.", DailyRequestLimit = 500, CurrentDayRequests = 42, HealthStatus = "Healthy", LastCheckedAtUtc = DateTime.UtcNow },
                new SourceDefinition { Id = "SRC-002", Name = "Public job board", Description = "Public indexing boards and technology work listings.", Category = "JobBoard", PolicyStatus = "Active", AllowsDirectFetch = true, TermsUrl = "https://vsrsystems.com/compliance/source-policy", AttributionNotice = "Referenced from public job board listing.", DailyRequestLimit = 300, CurrentDayRequests = 18, HealthStatus = "Healthy", LastCheckedAtUtc = DateTime.UtcNow },
                new SourceDefinition { Id = "SRC-003", Name = "Direct company research", Description = "Verified manual and automated deep research on target organizations.", Category = "Research", PolicyStatus = "Active", AllowsDirectFetch = true, TermsUrl = "https://vsrsystems.com/compliance/source-policy", AttributionNotice = "Verified research entry by sales team member.", DailyRequestLimit = 200, CurrentDayRequests = 9, HealthStatus = "Healthy", LastCheckedAtUtc = DateTime.UtcNow },
                new SourceDefinition { Id = "SRC-004", Name = "Public procurement", Description = "Government and public sector RFP / vendor registration portals.", Category = "Procurement", PolicyStatus = "Active", AllowsDirectFetch = true, TermsUrl = "https://vsrsystems.com/compliance/source-policy", AttributionNotice = "Public procurement notice / tender reference.", DailyRequestLimit = 150, CurrentDayRequests = 3, HealthStatus = "Healthy", LastCheckedAtUtc = DateTime.UtcNow },
                new SourceDefinition { Id = "SRC-005", Name = "Approved feed", Description = "Syndicated developer contract and technology requirement feeds.", Category = "Feed", PolicyStatus = "Active", AllowsDirectFetch = true, TermsUrl = "https://vsrsystems.com/compliance/source-policy", AttributionNotice = "Syndicated feed item with canonical URL.", DailyRequestLimit = 400, CurrentDayRequests = 24, HealthStatus = "Healthy", LastCheckedAtUtc = DateTime.UtcNow },
                new SourceDefinition { Id = "SRC-006", Name = "LinkedIn handoff", Description = "Manual browser handoff only. Automated scraping and automated DMs prohibited by policy.", Category = "Social", PolicyStatus = "Restricted", AllowsDirectFetch = false, TermsUrl = "https://vsrsystems.com/compliance/source-policy", AttributionNotice = "Manual external browser link only. No automated access.", DailyRequestLimit = 0, CurrentDayRequests = 0, HealthStatus = "Healthy", LastCheckedAtUtc = DateTime.UtcNow },
                new SourceDefinition { Id = "SRC-007", Name = "Hacker News (Freelance & Who's Hiring)", Description = "Live founder and engineering lead contractor postings from monthly Hacker News threads.", Category = "ContractBoard", PolicyStatus = "Active", AllowsDirectFetch = true, TermsUrl = "https://news.ycombinator.com", AttributionNotice = "Public Hacker News community thread with canonical story link.", DailyRequestLimit = 600, CurrentDayRequests = 12, HealthStatus = "Healthy", LastCheckedAtUtc = DateTime.UtcNow },
                new SourceDefinition { Id = "SRC-008", Name = "We Work Remotely (Contracts)", Description = "Live verified remote programming and software contract feeds from We Work Remotely.", Category = "ContractBoard", PolicyStatus = "Active", AllowsDirectFetch = true, TermsUrl = "https://weworkremotely.com", AttributionNotice = "Syndicated WWR RSS feed with canonical job URL.", DailyRequestLimit = 500, CurrentDayRequests = 15, HealthStatus = "Healthy", LastCheckedAtUtc = DateTime.UtcNow },
                new SourceDefinition { Id = "SRC-009", Name = "Remotive (Contract / Freelance)", Description = "Live contract-only software development projects and developer contractor roles from Remotive.", Category = "ContractBoard", PolicyStatus = "Active", AllowsDirectFetch = true, TermsUrl = "https://remotive.com", AttributionNotice = "Remotive public API with canonical job application URL.", DailyRequestLimit = 500, CurrentDayRequests = 8, HealthStatus = "Healthy", LastCheckedAtUtc = DateTime.UtcNow },
                new SourceDefinition { Id = "SRC-010", Name = "Jobicy (Developer Contracts)", Description = "Live remote engineering contracts and developer projects from Jobicy.", Category = "ContractBoard", PolicyStatus = "Active", AllowsDirectFetch = true, TermsUrl = "https://jobicy.com", AttributionNotice = "Jobicy Engineering API with direct job listing link.", DailyRequestLimit = 400, CurrentDayRequests = 5, HealthStatus = "Healthy", LastCheckedAtUtc = DateTime.UtcNow });
        }

        if (!await Any(context.Schedules, cancellationToken))
        {
            context.Schedules.AddRange(
                new AutomationSchedule
                {
                    Id = "SCHED-001",
                    Name = "Morning shift agent (06:00-14:00 IST)",
                    Description = "Live Hacker News and Remotive contract leads every 2 hours",
                    Frequency = "Interval",
                    IntervalHours = 2,
                    CronExpression = "0 */2 * * *",
                    Timezone = "Asia/Kolkata",
                    IsEnabled = true,
                    ProfileId = "PROF-001",
                    SelectedSources = new List<string> { "Hacker News (Freelance & Who's Hiring)", "Remotive (Contract / Freelance)" },
                    RunCap = 50,
                    QuietHoursStart = "14:00",
                    QuietHoursEnd = "06:00",
                    LastRunAtUtc = DateTime.UtcNow.AddHours(-16),
                    NextRunAtUtc = DateTime.UtcNow.AddMinutes(1),
                    LastRunStatus = "Success",
                    CreatedBy = "System",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-14),
                    UpdatedAtUtc = DateTime.UtcNow.AddHours(-16)
                },
                new AutomationSchedule
                {
                    Id = "SCHED-003",
                    Name = "Day shift agent (14:00-22:00 IST)",
                    Description = "Live We Work Remotely and Jobicy contract leads every 2 hours",
                    Frequency = "Interval",
                    IntervalHours = 2,
                    CronExpression = "0 */2 * * *",
                    Timezone = "Asia/Kolkata",
                    IsEnabled = true,
                    ProfileId = "PROF-001",
                    SelectedSources = new List<string> { "We Work Remotely (Contracts)", "Jobicy (Developer Contracts)" },
                    RunCap = 50,
                    QuietHoursStart = "22:00",
                    QuietHoursEnd = "14:00",
                    NextRunAtUtc = DateTime.UtcNow.AddMinutes(1),
                    LastRunStatus = "Ready",
                    CreatedBy = "System",
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                },
                new AutomationSchedule
                {
                    Id = "SCHED-004",
                    Name = "Night shift agent (22:00-06:00 IST)",
                    Description = "All four live contract feeds every 2 hours overnight",
                    Frequency = "Interval",
                    IntervalHours = 2,
                    CronExpression = "0 */2 * * *",
                    Timezone = "Asia/Kolkata",
                    IsEnabled = true,
                    ProfileId = "PROF-001",
                    SelectedSources = new List<string> { "Hacker News (Freelance & Who's Hiring)", "We Work Remotely (Contracts)", "Remotive (Contract / Freelance)", "Jobicy (Developer Contracts)" },
                    RunCap = 50,
                    QuietHoursStart = "06:00",
                    QuietHoursEnd = "22:00",
                    NextRunAtUtc = DateTime.UtcNow.AddMinutes(1),
                    LastRunStatus = "Ready",
                    CreatedBy = "System",
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                },
                new AutomationSchedule
                {
                    Id = "SCHED-002",
                    Name = "Weekly procurement scan",
                    Description = "Selected public procurement portals and tenders",
                    Frequency = "Weekly",
                    CronExpression = "0 9 * * 1",
                    Timezone = "Asia/Kolkata",
                    IsEnabled = false,
                    ProfileId = "PROF-002",
                    SelectedSources = new List<string> { "Public procurement" },
                    RunCap = 30,
                    QuietHoursStart = "22:00",
                    QuietHoursEnd = "07:00",
                    LastRunAtUtc = DateTime.UtcNow.AddDays(-6),
                    NextRunAtUtc = DateTime.UtcNow.AddDays(1),
                    LastRunStatus = "Success",
                    CreatedBy = "System",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-14),
                    UpdatedAtUtc = DateTime.UtcNow.AddDays(-6)
                });
        }

        if (!await Any(context.Campaigns, cancellationToken))
        {
            context.Campaigns.AddRange(
                new Campaign { Id = "CMP-001", Name = ".NET API Modernization Outreach", Audience = "Healthcare & Fintech CTOs", Mode = "ApprovalRequired", Status = "Active", DailySendLimit = 30, CurrentDaySent = 8, MaxTouches = 3, DelayDaysBetweenTouches = 3, StopOnReply = true, StopOnOptOut = true, StopOnBounce = true, SelectedTemplateId = "TPL-001", SenderName = "VSR Outreach Lead", SenderEmail = "outreach@vsrtechnologies.com", SenderAddress = "100 Enterprise Way, Suite 400, Austin TX 78701", Jurisdiction = "US-CANSPAM", OptOutMechanismUrl = "https://vsrtechnologies.com/outreach/opt-out", TotalSent = 142, TotalReplies = 18, TotalOptOuts = 2, CreatedBy = "System", CreatedAtUtc = DateTime.UtcNow.AddDays(-14), UpdatedAtUtc = DateTime.UtcNow.AddDays(-1) },
                new Campaign { Id = "CMP-002", Name = "Cloud Infrastructure Augmentation", Audience = "Scale-ups in US/UK", Mode = "DraftOnly", Status = "Paused", DailySendLimit = 20, CurrentDaySent = 0, MaxTouches = 2, DelayDaysBetweenTouches = 4, StopOnReply = true, StopOnOptOut = true, StopOnBounce = true, SelectedTemplateId = "TPL-002", SenderName = "VSR Client Partner", SenderEmail = "partners@vsrtechnologies.com", SenderAddress = "100 Enterprise Way, Suite 400, Austin TX 78701", Jurisdiction = "US-CANSPAM", OptOutMechanismUrl = "https://vsrtechnologies.com/outreach/opt-out", TotalSent = 89, TotalReplies = 7, TotalOptOuts = 1, CreatedBy = "System", CreatedAtUtc = DateTime.UtcNow.AddDays(-10), UpdatedAtUtc = DateTime.UtcNow.AddDays(-2) },
                new Campaign { Id = "CMP-003", Name = "Public Procurement Capability Brief", Audience = "Public Sector Tender Leads", Mode = "DraftOnly", Status = "Draft", DailySendLimit = 15, CurrentDaySent = 0, MaxTouches = 2, DelayDaysBetweenTouches = 5, StopOnReply = true, StopOnOptOut = true, StopOnBounce = true, SelectedTemplateId = "TPL-003", SenderName = "VSR Public Sector Lead", SenderEmail = "procurement@vsrtechnologies.com", SenderAddress = "100 Enterprise Way, Suite 400, Austin TX 78701", Jurisdiction = "US-CANSPAM", OptOutMechanismUrl = "https://vsrtechnologies.com/outreach/opt-out", TotalSent = 0, TotalReplies = 0, TotalOptOuts = 0, CreatedBy = "System", CreatedAtUtc = DateTime.UtcNow.AddDays(-5), UpdatedAtUtc = DateTime.UtcNow.AddDays(-5) });
        }

        if (!await Any(context.OutreachTemplates, cancellationToken))
        {
            context.OutreachTemplates.AddRange(
                new OutreachTemplate { Id = "TPL-001", Name = "Cloud Architecture & Modernization Introduction", Category = "Cloud & Architecture", SubjectTemplate = "Inquiry regarding {{CompanyName}}'s {{RoleOrOpportunity}} initiative", BodyTemplate = "Hi {{ContactName}},\n\nI noticed {{CompanyName}} is exploring {{RoleOrOpportunity}} (via {{SourceContext}}).\n\nAt {{SenderCompany}}, we specialize in {{ServiceProposition}}.\n\nWould you be open to a brief 10-minute introductory conversation next week to explore how we can support your delivery schedule?\n\nBest regards,\n{{SenderName}}\n{{SenderTitle}}\n{{SenderCompany}}\n\n{{JurisdictionFooter}}", ServiceProposition = "cloud-native migration, enterprise API modernization, and scalable full-stack engineering", JurisdictionPolicy = "US-CANSPAM", FooterTemplate = "To opt out of future updates, reply 'UNSUBSCRIBE' or visit {{OptOutUrl}}.\n{{SenderCompany}}, 100 Enterprise Way, Suite 400, Austin TX 78701.", IsActive = true, CreatedBy = "System", CreatedAtUtc = DateTime.UtcNow.AddDays(-7), UpdatedAtUtc = DateTime.UtcNow.AddDays(-7) },
                new OutreachTemplate { Id = "TPL-002", Name = "Engineering Team Augmentation", Category = "Team Augmentation", SubjectTemplate = "{{CompanyName}}'s technical team capacity for {{RoleOrOpportunity}}", BodyTemplate = "Hi {{ContactName}},\n\nI saw that {{CompanyName}} is looking to expand engineering capability for {{RoleOrOpportunity}} (referenced in {{SourceContext}}).\n\n{{SenderCompany}} provides pre-vetted senior software engineers across .NET, React, and cloud infrastructure ready to integrate directly into your sprint cycles within 5 business days.\n\nCould we schedule a quick call this week to review your technical requirements?\n\nBest regards,\n{{SenderName}}\n{{SenderTitle}}\n{{SenderCompany}}\n\n{{JurisdictionFooter}}", ServiceProposition = "high-velocity senior team augmentation for backend .NET, cloud DevOps, and frontend React architectures", JurisdictionPolicy = "US-CANSPAM", FooterTemplate = "To opt out of future updates, reply 'UNSUBSCRIBE' or visit {{OptOutUrl}}.\n{{SenderCompany}}, 100 Enterprise Way, Suite 400, Austin TX 78701.", IsActive = true, CreatedBy = "System", CreatedAtUtc = DateTime.UtcNow.AddDays(-6), UpdatedAtUtc = DateTime.UtcNow.AddDays(-6) },
                new OutreachTemplate { Id = "TPL-003", Name = "Public Procurement & RFP Capability Brief", Category = "Procurement", SubjectTemplate = "Capability Statement: {{CompanyName}} - {{RoleOrOpportunity}}", BodyTemplate = "Dear {{ContactName}},\n\nRegarding the recent public posting for {{RoleOrOpportunity}} (source: {{SourceContext}}), {{SenderCompany}} is pleased to submit our commercial capability brief.\n\nOur engineering practice specializes in {{ServiceProposition}}, and we maintain standard compliance readiness documentation for enterprise and public vendor onboarding.\n\nPlease let us know the appropriate submission point of contact or portal to transmit our full vendor packet.\n\nSincerely,\n{{SenderName}}\n{{SenderTitle}}\n{{SenderCompany}}\n\n{{JurisdictionFooter}}", ServiceProposition = "enterprise software architecture, secure cloud migrations, and compliant government/commercial subcontracting", JurisdictionPolicy = "US-CANSPAM", FooterTemplate = "To opt out of future updates, reply 'UNSUBSCRIBE' or visit {{OptOutUrl}}.\n{{SenderCompany}}, 100 Enterprise Way, Suite 400, Austin TX 78701.", IsActive = true, CreatedBy = "System", CreatedAtUtc = DateTime.UtcNow.AddDays(-5), UpdatedAtUtc = DateTime.UtcNow.AddDays(-5) },
                new OutreachTemplate { Id = "TPL-004", Name = "EU GDPR Compliant B2B Direct Inquiry", Category = "Cloud & Architecture", SubjectTemplate = "B2B Technical Inquiry for {{CompanyName}} (re: {{RoleOrOpportunity}})", BodyTemplate = "Dear {{ContactName}},\n\nI am contacting you regarding {{CompanyName}}'s publicly listed opening for {{RoleOrOpportunity}}.\n\n{{SenderCompany}} assists technical organizations with {{ServiceProposition}}.\n\nIf this aligns with your current roadmap, we would be pleased to share a relevant architectural case study.\n\nKind regards,\n{{SenderName}}\n{{SenderTitle}}\n{{SenderCompany}}\n\n{{JurisdictionFooter}}", ServiceProposition = "cloud modernization and high-availability enterprise services", JurisdictionPolicy = "EU-GDPR", FooterTemplate = "You are receiving this B2B communication on the basis of legitimate interest related to your public professional role. To exercise your right to object or opt out at any time, reply 'UNSUBSCRIBE' or visit {{OptOutUrl}}.\nController: {{SenderCompany}}, 100 Enterprise Way, Suite 400, Austin TX 78701.", IsActive = true, CreatedBy = "System", CreatedAtUtc = DateTime.UtcNow.AddDays(-4), UpdatedAtUtc = DateTime.UtcNow.AddDays(-4) });
        }

        if (!await Any(context.Suppressions, cancellationToken))
        {
            context.Suppressions.AddRange(
                new SuppressionRecord { Id = "SUP-001", Target = "optout@example.com", Type = "Email", Reason = "OptOut", Source = "UnsubscribeLink", CreatedBy = "System", CreatedAtUtc = DateTime.UtcNow.AddDays(-10) },
                new SuppressionRecord { Id = "SUP-002", Target = "do-not-contact@prospect.org", Type = "Email", Reason = "OptOut", Source = "UserRequest", CreatedBy = "System", CreatedAtUtc = DateTime.UtcNow.AddDays(-5) },
                new SuppressionRecord { Id = "SUP-003", Target = "blocked-competitor.example", Type = "Domain", Reason = "BlockedByPolicy", Source = "AdminAction", CreatedBy = "System", CreatedAtUtc = DateTime.UtcNow.AddDays(-20) });
        }

        if (!await Any(context.Users, cancellationToken))
        {
            context.Users.AddRange(
                new User { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Email = "admin@vsrsalesengine.com", PasswordHash = "Admin123!", FullName = "VSR Admin", Role = "Admin", IsGuest = false, CreatedAtUtc = DateTime.UtcNow },
                new User { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Email = "sales@vsrsalesengine.com", PasswordHash = "Sales123!", FullName = "VSR Sales Team", Role = "SalesMember", IsGuest = false, CreatedAtUtc = DateTime.UtcNow });
        }

        if (!await Any(context.Executions, cancellationToken))
        {
            context.Executions.AddRange(
                new ExecutionRecord { Id = "EXEC-001", JobType = "Discovery", ReferenceId = "SCHED-001", Status = "Succeeded", FailureClassification = "None", AttemptCount = 1, MaxAttempts = 3, DurationMs = 1420, CreatedAtUtc = DateTime.UtcNow.AddHours(-4), CompletedAtUtc = DateTime.UtcNow.AddHours(-4).AddSeconds(1.42) },
                new ExecutionRecord { Id = "EXEC-002", JobType = "Discovery", ReferenceId = "SRC-001", Status = "FailedTransient", FailureClassification = "Transient", AttemptCount = 1, MaxAttempts = 3, ErrorMessage = "HTTP 429 Too Many Requests: Upstream rate limit reached on company careers portal", NextRetryAtUtc = DateTime.UtcNow.AddMinutes(5), DurationMs = 650, CreatedAtUtc = DateTime.UtcNow.AddHours(-1), CompletedAtUtc = DateTime.UtcNow.AddHours(-1).AddSeconds(0.65) },
                new ExecutionRecord { Id = "EXEC-003", JobType = "OutreachSend", ReferenceId = "CMP-001", Status = "DeadLetter", FailureClassification = "Permanent", AttemptCount = 1, MaxAttempts = 1, ErrorMessage = "Suppression rule triggered: Domain @blocked-client.example is on the exclusion list", DurationMs = 120, CreatedAtUtc = DateTime.UtcNow.AddHours(-2), CompletedAtUtc = DateTime.UtcNow.AddHours(-2).AddSeconds(0.12) });

            context.SourceFailureCounts.Add(new SourceFailureCount { SourceName = "Company careers", Count = 1 });
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task<bool> Any<T>(Microsoft.EntityFrameworkCore.DbSet<T> set, CancellationToken cancellationToken) where T : class
    {
        return await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(set, cancellationToken);
    }
}
