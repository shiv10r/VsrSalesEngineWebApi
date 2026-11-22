using Microsoft.EntityFrameworkCore;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Infrastructure.Data;

namespace VsrSalesEngineWebApi.Tests;

/// <summary>
/// Test-only fixtures. Production seeding creates configuration only; these invented rows exist so tests
/// have opportunities, contacts, campaigns, executions and suppressions to work with.
/// </summary>
public static class TestFixtureData
{
    public const string NoAdapterSource = "Test source (no adapter)";

    public static async Task AddAsync(VsrDbContext context, CancellationToken cancellationToken = default)
    {
        if (!await Any(context.Opportunities, cancellationToken))
        {
            context.Opportunities.Add(new Opportunity
            {
                Id = "OP-1001",
                Company = "Northstar Digital",
                Title = "Senior .NET / React Engineer",
                Source = TestFixtureData.NoAdapterSource,
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
                Source = TestFixtureData.NoAdapterSource,
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

        if (!await Any(context.Campaigns, cancellationToken))
        {
            context.Campaigns.AddRange(
                new Campaign { Id = "CMP-001", Name = ".NET API Modernization Outreach", Audience = "Healthcare & Fintech CTOs", Mode = "ApprovalRequired", Status = "Active", DailySendLimit = 30, CurrentDaySent = 8, MaxTouches = 3, DelayDaysBetweenTouches = 3, StopOnReply = true, StopOnOptOut = true, StopOnBounce = true, SelectedTemplateId = "TPL-001", SenderName = "VSR Outreach Lead", SenderEmail = "outreach@vsrtechnologies.com", SenderAddress = "100 Enterprise Way, Suite 400, Austin TX 78701", Jurisdiction = "US-CANSPAM", OptOutMechanismUrl = "https://vsrtechnologies.com/outreach/opt-out", TotalSent = 142, TotalReplies = 18, TotalOptOuts = 2, CreatedBy = "System", CreatedAtUtc = DateTime.UtcNow.AddDays(-14), UpdatedAtUtc = DateTime.UtcNow.AddDays(-1) },
                new Campaign { Id = "CMP-002", Name = "Cloud Infrastructure Augmentation", Audience = "Scale-ups in US/UK", Mode = "DraftOnly", Status = "Paused", DailySendLimit = 20, CurrentDaySent = 0, MaxTouches = 2, DelayDaysBetweenTouches = 4, StopOnReply = true, StopOnOptOut = true, StopOnBounce = true, SelectedTemplateId = "TPL-002", SenderName = "VSR Client Partner", SenderEmail = "partners@vsrtechnologies.com", SenderAddress = "100 Enterprise Way, Suite 400, Austin TX 78701", Jurisdiction = "US-CANSPAM", OptOutMechanismUrl = "https://vsrtechnologies.com/outreach/opt-out", TotalSent = 89, TotalReplies = 7, TotalOptOuts = 1, CreatedBy = "System", CreatedAtUtc = DateTime.UtcNow.AddDays(-10), UpdatedAtUtc = DateTime.UtcNow.AddDays(-2) },
                new Campaign { Id = "CMP-003", Name = "Public Procurement Capability Brief", Audience = "Public Sector Tender Leads", Mode = "DraftOnly", Status = "Draft", DailySendLimit = 15, CurrentDaySent = 0, MaxTouches = 2, DelayDaysBetweenTouches = 5, StopOnReply = true, StopOnOptOut = true, StopOnBounce = true, SelectedTemplateId = "TPL-003", SenderName = "VSR Public Sector Lead", SenderEmail = "procurement@vsrtechnologies.com", SenderAddress = "100 Enterprise Way, Suite 400, Austin TX 78701", Jurisdiction = "US-CANSPAM", OptOutMechanismUrl = "https://vsrtechnologies.com/outreach/opt-out", TotalSent = 0, TotalReplies = 0, TotalOptOuts = 0, CreatedBy = "System", CreatedAtUtc = DateTime.UtcNow.AddDays(-5), UpdatedAtUtc = DateTime.UtcNow.AddDays(-5) });
        }

        if (!await Any(context.Suppressions, cancellationToken))
        {
            context.Suppressions.AddRange(
                new SuppressionRecord { Id = "SUP-001", Target = "optout@example.com", Type = "Email", Reason = "OptOut", Source = "UnsubscribeLink", CreatedBy = "System", CreatedAtUtc = DateTime.UtcNow.AddDays(-10) },
                new SuppressionRecord { Id = "SUP-002", Target = "do-not-contact@prospect.org", Type = "Email", Reason = "OptOut", Source = "UserRequest", CreatedBy = "System", CreatedAtUtc = DateTime.UtcNow.AddDays(-5) },
                new SuppressionRecord { Id = "SUP-003", Target = "blocked-competitor.example", Type = "Domain", Reason = "BlockedByPolicy", Source = "AdminAction", CreatedBy = "System", CreatedAtUtc = DateTime.UtcNow.AddDays(-20) });
        }

        if (!await Any(context.Executions, cancellationToken))
        {
            context.Executions.AddRange(
                new ExecutionRecord { Id = "EXEC-001", JobType = "Discovery", ReferenceId = "SCHED-001", Status = "Succeeded", FailureClassification = "None", AttemptCount = 1, MaxAttempts = 3, DurationMs = 1420, CreatedAtUtc = DateTime.UtcNow.AddHours(-4), CompletedAtUtc = DateTime.UtcNow.AddHours(-4).AddSeconds(1.42) },
                new ExecutionRecord { Id = "EXEC-002", JobType = "Discovery", ReferenceId = "SRC-001", Status = "FailedTransient", FailureClassification = "Transient", AttemptCount = 1, MaxAttempts = 3, ErrorMessage = "HTTP 429 Too Many Requests: Upstream rate limit reached on company careers portal", NextRetryAtUtc = DateTime.UtcNow.AddMinutes(5), DurationMs = 650, CreatedAtUtc = DateTime.UtcNow.AddHours(-1), CompletedAtUtc = DateTime.UtcNow.AddHours(-1).AddSeconds(0.65) },
                new ExecutionRecord { Id = "EXEC-003", JobType = "OutreachSend", ReferenceId = "CMP-001", Status = "DeadLetter", FailureClassification = "Permanent", AttemptCount = 1, MaxAttempts = 1, ErrorMessage = "Suppression rule triggered: Domain @blocked-client.example is on the exclusion list", DurationMs = 120, CreatedAtUtc = DateTime.UtcNow.AddHours(-2), CompletedAtUtc = DateTime.UtcNow.AddHours(-2).AddSeconds(0.12) });

            context.SourceFailureCounts.Add(new SourceFailureCount { SourceName = TestFixtureData.NoAdapterSource, Count = 1 });
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static Task<bool> Any<T>(DbSet<T> set, CancellationToken cancellationToken) where T : class =>
        set.AnyAsync(cancellationToken);
}
