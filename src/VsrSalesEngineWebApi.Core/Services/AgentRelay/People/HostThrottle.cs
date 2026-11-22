namespace VsrSalesEngineWebApi.Core.Services.AgentRelay.People;

/// <summary>At most one request per second to the same host.</summary>
public sealed class HostThrottle
{
    private readonly object _gate = new();
    private readonly Dictionary<string, DateTime> _next = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan _gap;
    private readonly Func<DateTime> _now;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public HostThrottle() : this(TimeSpan.FromSeconds(1), () => DateTime.UtcNow, Task.Delay) { }

    public HostThrottle(TimeSpan gap, Func<DateTime> now, Func<TimeSpan, CancellationToken, Task> delay)
    {
        _gap = gap;
        _now = now;
        _delay = delay;
    }

    public Task WaitAsync(string host, CancellationToken cancellationToken)
    {
        TimeSpan wait;
        lock (_gate)
        {
            var now = _now();
            var slot = _next.TryGetValue(host, out var next) && next > now ? next : now;
            _next[host] = slot + _gap;
            wait = slot - now;
        }
        return wait > TimeSpan.Zero ? _delay(wait, cancellationToken) : Task.CompletedTask;
    }
}
