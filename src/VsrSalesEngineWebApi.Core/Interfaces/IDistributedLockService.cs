namespace VsrSalesEngineWebApi.Core.Interfaces;

public interface IDistributedLockService
{
    Task<bool> TryAcquireLockAsync(string lockKey, string ownerId, TimeSpan duration);
    Task ReleaseLockAsync(string lockKey, string ownerId);
    Task<bool> IsLockedAsync(string lockKey);
}
