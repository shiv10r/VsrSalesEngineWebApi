using Microsoft.EntityFrameworkCore;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Security;

namespace VsrSalesEngineWebApi.Infrastructure.Data;

/// <summary>
/// Seeds configuration only: the source registry, the default search profile, outreach templates, the three
/// shift schedules and (Development only) demo users. It never invents business data: no opportunities,
/// contacts, campaigns, executions or suppressions, and no invented counters.
/// </summary>
public static class VsrDbSeeder
{
    public static async Task SeedAsync(VsrDbContext context, UserSeedOptions? users = null, CancellationToken cancellationToken = default)
    {
        await LegacyDemoDataCleaner.RemoveAsync(context, cancellationToken);

        if (!await context.SearchProfiles.AnyAsync(cancellationToken))
        {
            context.SearchProfiles.Add(DefaultProfile());
        }

        if (!await context.Sources.AnyAsync(cancellationToken)) context.Sources.AddRange(SourceRegistrySeed.All());
        if (!await context.Schedules.AnyAsync(cancellationToken)) context.Schedules.AddRange(ShiftScheduleSeed.All());
        if (!await context.OutreachTemplates.AnyAsync(cancellationToken)) context.OutreachTemplates.AddRange(OutreachTemplateSeed.All());

        await SeedUsersAsync(context, users ?? UserSeedOptions.None, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>One generic profile. Keywords are plain software-service terms the owner can edit in Agent settings.</summary>
    private static SearchProfile DefaultProfile() => new()
    {
        Id = "PRF-001",
        Name = "Software development leads",
        TargetKeywords = ".NET, C#, React, web application, mobile app, ERP, software development",
        ExcludedKeywords = "internship, unpaid",
        Location = "Uttar Pradesh, India",
        OpportunityType = "All relevant types",
        PublishedWithinDays = 14,
        SelectedSources = new List<string>(),
        MaxResults = 50,
        IsActive = true,
        CreatedBy = "System",
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow
    };

    // Demo passwords are hashed once per process (PBKDF2 is deliberately slow); every user still gets its own salt.
    private static readonly Lazy<(string Admin, string Sales)> DemoHashes = new(() =>
        (PasswordHasher.Hash("Admin123!"), PasswordHasher.Hash("Sales123!")));

    private static async Task SeedUsersAsync(VsrDbContext context, UserSeedOptions options, CancellationToken cancellationToken)
    {
        if (options.DemoUsers)
        {
            // Development only. These logins are public in the repository.
            if (await context.Users.AnyAsync(cancellationToken)) return;

            context.Users.AddRange(
                new User { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Email = "admin@vsrsalesengine.com", PasswordHash = DemoHashes.Value.Admin, FullName = "VSR Admin", Role = "Admin", IsGuest = false, CreatedAtUtc = DateTime.UtcNow },
                new User { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Email = "sales@vsrsalesengine.com", PasswordHash = DemoHashes.Value.Sales, FullName = "VSR Sales Team", Role = "SalesMember", IsGuest = false, CreatedAtUtc = DateTime.UtcNow });
            return;
        }

        if (string.IsNullOrWhiteSpace(options.AdminEmail) || string.IsNullOrEmpty(options.AdminPassword)) return;

        var email = options.AdminEmail.Trim().ToLowerInvariant();
        var exists = await context.Users.AnyAsync(u => u.Email.ToLower() == email, cancellationToken);
        if (exists) return;

        context.Users.Add(new User
        {
            Email = email,
            PasswordHash = PasswordHasher.Hash(options.AdminPassword),
            FullName = "Administrator",
            Role = "Admin",
            IsGuest = false,
            CreatedAtUtc = DateTime.UtcNow
        });
    }

}
