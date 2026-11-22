namespace VsrSalesEngineWebApi.Core.Services.LocalLeads;

/// <summary>
/// Counts Google Places requests per UTC day in memory so a mistake or abuse cannot run up the owner's bill.
/// The count resets on restart, so the Google Cloud budget alert stays the real backstop.
/// </summary>
public class PlacesUsageLimiter
{
    private readonly object _gate = new();
    private readonly Func<DateTime> _utcNow;
    private DateOnly _day;
    private int _used;

    public int DailyCap { get; }

    public PlacesUsageLimiter(GooglePlacesOptions options, Func<DateTime>? utcNow = null)
    {
        DailyCap = options.DailyRequestCap > 0 ? options.DailyRequestCap : GooglePlacesOptions.DefaultDailyCap;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
        _day = DateOnly.FromDateTime(_utcNow());
    }

    public int Remaining
    {
        get
        {
            lock (_gate)
            {
                Roll();
                return Math.Max(0, DailyCap - _used);
            }
        }
    }

    /// <summary>Takes one request from today's budget. False when the cap is already reached.</summary>
    public bool TryAcquire()
    {
        lock (_gate)
        {
            Roll();
            if (_used >= DailyCap) return false;
            _used++;
            return true;
        }
    }

    private void Roll()
    {
        var today = DateOnly.FromDateTime(_utcNow());
        if (today == _day) return;
        _day = today;
        _used = 0;
    }
}
