namespace VsrSalesEngineWebApi.Core.Entities;

public class DistributedRunLock
{
    public string LockKey { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public DateTime AcquiredAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddMinutes(5);
    public bool IsReleased { get; set; } = false;
}
