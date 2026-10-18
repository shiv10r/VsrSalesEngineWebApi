namespace VsrSalesEngineWebApi.Core.Entities;

public class SinkMessage
{
    public string Id { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string OriginalRecipient { get; set; } = string.Empty;
    public string RoutedRecipient { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string ProviderMessageId { get; set; } = string.Empty;
    public string Mode { get; set; } = "SandboxSink"; // SandboxSink, SimulatedProvider
    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
}
