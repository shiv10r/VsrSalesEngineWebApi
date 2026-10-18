using System.Collections.Concurrent;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Core.Services;

public class DistributedLockService : IDistributedLockService
{
    private static readonly ConcurrentDictionary<string, DistributedRunLock> Locks = new(StringComparer.OrdinalIgnoreCase);

    public Task<bool> TryAcquireLockAsync(string lockKey, string ownerId, TimeSpan duration)
    {
        var now = DateTime.UtcNow;

        while (true)
        {
            if (Locks.TryGetValue(lockKey, out var existing))
            {
                if (!existing.IsReleased && existing.ExpiresAtUtc > now && existing.OwnerId != ownerId)
                {
                    // Locked by someone else and lease is still valid
                    return Task.FromResult(false);
                }

                // Lock is released or expired, try replace atomically
                var newLock = new DistributedRunLock
                {
                    LockKey = lockKey,
                    OwnerId = ownerId,
                    AcquiredAtUtc = now,
                    ExpiresAtUtc = now.Add(duration),
                    IsReleased = false
                };

                if (Locks.TryUpdate(lockKey, newLock, existing))
                {
                    return Task.FromResult(true);
                }
            }
            else
            {
                // No lock present, try add
                var newLock = new DistributedRunLock
                {
                    LockKey = lockKey,
                    OwnerId = ownerId,
                    AcquiredAtUtc = now,
                    ExpiresAtUtc = now.Add(duration),
                    IsReleased = false
                };

                if (Locks.TryAdd(lockKey, newLock))
                {
                    return Task.FromResult(true);
                }
            }
        }
    }

    public Task ReleaseLockAsync(string lockKey, string ownerId)
    {
        if (Locks.TryGetValue(lockKey, out var existing) && existing.OwnerId == ownerId)
        {
            existing.IsReleased = true;
        }
        return Task.CompletedTask;
    }

    public Task<bool> IsLockedAsync(string lockKey)
    {
        if (Locks.TryGetValue(lockKey, out var existing))
        {
            var isLocked = !existing.IsReleased && existing.ExpiresAtUtc > DateTime.UtcNow;
            return Task.FromResult(isLocked);
        }
        return Task.FromResult(false);
    }
}
