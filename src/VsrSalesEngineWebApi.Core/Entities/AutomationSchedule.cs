namespace VsrSalesEngineWebApi.Core.Entities;

public class AutomationSchedule
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Frequency { get; set; } = "Daily"; // Daily, Weekly, CustomCron
    public string CronExpression { get; set; } = "0 8 * * 1-5";
    public string Timezone { get; set; } = "Asia/Kolkata";
    public bool IsEnabled { get; set; } = true;
    public string ProfileId { get; set; } = "DEFAULT_PROFILE";
    public List<string> SelectedSources { get; set; } = new();
    public int RunCap { get; set; } = 50;
    public string QuietHoursStart { get; set; } = "22:00";
    public string QuietHoursEnd { get; set; } = "07:00";
    public DateTime? LastRunAtUtc { get; set; }
    public DateTime NextRunAtUtc { get; set; } = DateTime.UtcNow.AddHours(12);
    public string LastRunStatus { get; set; } = "Ready"; // Ready, Running, Success, Failed, SkippedQuietHours
    public string CreatedBy { get; set; } = "Sales Member";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
