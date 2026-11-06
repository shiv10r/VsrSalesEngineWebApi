using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VsrSalesEngineWebApi.Core.Entities;

namespace VsrSalesEngineWebApi.Infrastructure.Data;

public class VsrDbContext : DbContext
{
    public VsrDbContext(DbContextOptions<VsrDbContext> options) : base(options) { }

    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<SearchProfile> SearchProfiles => Set<SearchProfile>();
    public DbSet<SourceDefinition> Sources => Set<SourceDefinition>();
    public DbSet<AutomationSchedule> Schedules => Set<AutomationSchedule>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<OutreachTemplate> OutreachTemplates => Set<OutreachTemplate>();
    public DbSet<SuppressionRecord> Suppressions => Set<SuppressionRecord>();
    public DbSet<User> Users => Set<User>();
    public DbSet<ExecutionRecord> Executions => Set<ExecutionRecord>();
    public DbSet<DistributedRunLock> Locks => Set<DistributedRunLock>();
    public DbSet<SinkMessage> SinkMessages => Set<SinkMessage>();
    public DbSet<SourceFailureCount> SourceFailureCounts => Set<SourceFailureCount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var stringListComparer = new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<string>>(
            (a, b) => (a ?? new()).SequenceEqual(b ?? new()),
            v => v == null ? 0 : v.Aggregate(0, (hash, s) => HashCode.Combine(hash, s.GetHashCode())),
            v => v == null ? new() : v.ToList());

        var statusHistoryComparer = new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<OpportunityStatusChange>>(
            (a, b) => JsonSerializer.Serialize(a) == JsonSerializer.Serialize(b),
            v => v == null ? 0 : JsonSerializer.Serialize(v).GetHashCode(),
            v => v == null ? new() : JsonSerializer.Deserialize<List<OpportunityStatusChange>>(JsonSerializer.Serialize(v))!);

        modelBuilder.Entity<Opportunity>(e =>
        {
            e.HasKey(o => o.Id);
            e.Property(o => o.StatusHistory)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<OpportunityStatusChange>>(v, (JsonSerializerOptions?)null) ?? new())
                .Metadata.SetValueComparer(statusHistoryComparer);
        });

        modelBuilder.Entity<Contact>(e => e.HasKey(c => c.Id));

        modelBuilder.Entity<SearchProfile>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.SelectedSources)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new())
                .Metadata.SetValueComparer(stringListComparer);
        });

        modelBuilder.Entity<SourceDefinition>(e => e.HasKey(s => s.Id));

        modelBuilder.Entity<AutomationSchedule>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.SelectedSources)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new())
                .Metadata.SetValueComparer(stringListComparer);
        });

        modelBuilder.Entity<Campaign>(e => e.HasKey(c => c.Id));
        modelBuilder.Entity<OutreachTemplate>(e => e.HasKey(t => t.Id));
        modelBuilder.Entity<SuppressionRecord>(e => e.HasKey(s => s.Id));
        modelBuilder.Entity<User>(e => e.HasKey(u => u.Id));
        modelBuilder.Entity<ExecutionRecord>(e => e.HasKey(r => r.Id));
        modelBuilder.Entity<DistributedRunLock>(e =>
        {
            e.HasKey(l => l.LockKey);
            e.Property(l => l.Version).IsConcurrencyToken();
        });
        modelBuilder.Entity<SinkMessage>(e => e.HasKey(m => m.Id));
        modelBuilder.Entity<SourceFailureCount>(e => e.HasKey(f => f.SourceName));
    }
}
