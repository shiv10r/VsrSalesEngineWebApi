using System.Collections.Concurrent;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Infrastructure.Repositories;

public class SuppressionRepository : ISuppressionRepository
{
    private static readonly ConcurrentDictionary<string, SuppressionRecord> Suppressions = new(StringComparer.OrdinalIgnoreCase);

    static SuppressionRepository()
    {
        var seed = new List<SuppressionRecord>
        {
            new()
            {
                Id = "SUP-001",
                Target = "optout@example.com",
                Type = "Email",
                Reason = "OptOut",
                Source = "UnsubscribeLink",
                CreatedBy = "System",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-10)
            },
            new()
            {
                Id = "SUP-002",
                Target = "do-not-contact@prospect.org",
                Type = "Email",
                Reason = "OptOut",
                Source = "UserRequest",
                CreatedBy = "System",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-5)
            },
            new()
            {
                Id = "SUP-003",
                Target = "blocked-competitor.example",
                Type = "Domain",
                Reason = "BlockedByPolicy",
                Source = "AdminAction",
                CreatedBy = "System",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-20)
            }
        };

        foreach (var s in seed)
        {
            Suppressions[s.Id] = s;
        }
    }

    public Task<IReadOnlyList<SuppressionRecord>> GetAllAsync(string? query = null)
    {
        var q = Suppressions.Values.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            q = q.Where(s => s.Target.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                             s.Reason.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        var list = q.OrderByDescending(s => s.CreatedAtUtc).ToList();
        return Task.FromResult<IReadOnlyList<SuppressionRecord>>(list);
    }

    public Task<SuppressionRecord?> GetByIdAsync(string id)
    {
        Suppressions.TryGetValue(id, out var record);
        return Task.FromResult(record);
    }

    public Task<bool> IsSuppressedAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return Task.FromResult(false);

        var normalizedEmail = email.Trim().ToLowerInvariant();

        // 1. Check exact email suppression
        var directMatch = Suppressions.Values.Any(s =>
            s.Type.Equals("Email", StringComparison.OrdinalIgnoreCase) &&
            s.Target.Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase));

        if (directMatch) return Task.FromResult(true);

        // 2. Check domain suppression
        var atIndex = normalizedEmail.IndexOf('@');
        if (atIndex >= 0 && atIndex < normalizedEmail.Length - 1)
        {
            var domain = normalizedEmail[(atIndex + 1)..];
            var domainMatch = Suppressions.Values.Any(s =>
                s.Type.Equals("Domain", StringComparison.OrdinalIgnoreCase) &&
                (s.Target.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
                 s.Target.Equals("@" + domain, StringComparison.OrdinalIgnoreCase)));

            if (domainMatch) return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task<SuppressionRecord> AddAsync(SuppressionRecord record)
    {
        Suppressions[record.Id] = record;
        return Task.FromResult(record);
    }

    public Task<bool> RemoveAsync(string id)
    {
        var removed = Suppressions.TryRemove(id, out _);
        return Task.FromResult(removed);
    }
}
