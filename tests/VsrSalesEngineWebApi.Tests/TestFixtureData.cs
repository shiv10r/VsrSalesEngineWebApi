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

        await context.SaveChangesAsync(cancellationToken);
    }

    private static Task<bool> Any<T>(DbSet<T> set, CancellationToken cancellationToken) where T : class =>
        set.AnyAsync(cancellationToken);
}
