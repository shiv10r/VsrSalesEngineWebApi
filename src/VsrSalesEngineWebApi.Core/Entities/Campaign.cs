namespace VsrSalesEngineWebApi.Core.Entities;

public class Campaign
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Mode { get; set; } = "DraftOnly"; // DraftOnly, ApprovalRequired, AutoSendWithinRules
    public string Status { get; set; } = "Draft"; // Draft, Active, Paused, Completed
    public int DailySendLimit { get; set; } = 25;
    public int CurrentDaySent { get; set; } = 0;
    public int MaxTouches { get; set; } = 3;
    public int DelayDaysBetweenTouches { get; set; } = 3;
    public bool StopOnReply { get; set; } = true;
    public bool StopOnOptOut { get; set; } = true;
    public bool StopOnBounce { get; set; } = true;
    public string SelectedTemplateId { get; set; } = "TPL-001";
    public string SenderName { get; set; } = "VSR Outreach Lead";
    public string SenderEmail { get; set; } = "outreach@vsrtechnologies.com";
    public string SenderAddress { get; set; } = "100 Enterprise Way, Suite 400, Austin TX 78701";
    public string Jurisdiction { get; set; } = "US-CANSPAM"; // US-CANSPAM, EU-GDPR, UK-PECR
    public string OptOutMechanismUrl { get; set; } = "https://vsrtechnologies.com/outreach/opt-out";
    public int TotalSent { get; set; } = 0;
    public int TotalReplies { get; set; } = 0;
    public int TotalOptOuts { get; set; } = 0;
    public string CreatedBy { get; set; } = "Sales Member";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
