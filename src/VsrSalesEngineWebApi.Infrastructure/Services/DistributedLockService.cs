using Microsoft.EntityFrameworkCore;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Infrastructure.Data;

namespace VsrSalesEngineWebApi.Infrastructure.Services;

public class DistributedLockService : IDistributedLockService
{
    private readonly VsrDbContext _context;

    public DistributedLockService(VsrDbContext context)
    {
        _context = context;
    }

    public async Task<bool> TryAcquireLockAsync(string lockKey, string ownerId, TimeSpan duration)
    {
        var now = DateTime.UtcNow;
        var existing = await _context.Locks.FirstOrDefaultAsync(l => l.LockKey == lockKey);

        if (existing == null)
        {
            _context.Locks.Add(new DistributedRunLock
            {
                LockKey = lockKey,
                OwnerId = ownerId,
                AcquiredAtUtc = now,
                ExpiresAtUtc = now.Add(duration),
                IsReleased = false
            });

            try
            {
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException)
            {
                // Another process won the race to insert the row.
                return false;
            }
        }

        var expired = existing.IsReleased || existing.ExpiresAtUtc <= now;
        if (!expired) return false;

        existing.OwnerId = ownerId;
        existing.AcquiredAtUtc = now;
        existing.ExpiresAtUtc = now.Add(duration);
        existing.IsReleased = false;
        existing.Version += 1;

        try
        {
            await _context.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    public async Task ReleaseLockAsync(string lockKey, string ownerId)
    {
        var existing = await _context.Locks.FirstOrDefaultAsync(l => l.LockKey == lockKey && l.OwnerId == ownerId);
        if (existing == null) return;

        existing.IsReleased = true;
        await _context.SaveChangesAsync();
    }

    public async Task<bool> IsLockedAsync(string lockKey)
    {
        var existing = await _context.Locks.AsNoTracking().FirstOrDefaultAsync(l => l.LockKey == lockKey);
        if (existing == null) return false;

        return !existing.IsReleased && existing.ExpiresAtUtc > DateTime.UtcNow;
    }
}
