using System.Collections.Concurrent;
using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Core.Services;

public class OutreachSenderService : IOutreachSender
{
    private readonly ISuppressionRepository _suppressionRepository;
    private static readonly ConcurrentDictionary<string, OutreachSendResult> IdempotencyStore = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentBag<SinkMessage> SinkStore = new();

    public const string DefaultSinkAddress = "dev-sink@vsrsalesengine.local";

    public OutreachSenderService(ISuppressionRepository suppressionRepository)
    {
        _suppressionRepository = suppressionRepository;
    }

    public static bool IsKillSwitchEngaged { get; set; } = false;
    public static Func<string, string?> GetEnvironmentVariable { get; set; } = Environment.GetEnvironmentVariable;

    public static bool CheckKillSwitchActive()
    {
        return IsKillSwitchEngaged || string.Equals(GetEnvironmentVariable("OUTREACH_KILL_SWITCH"), "true", StringComparison.OrdinalIgnoreCase);
    }

    public static void ResetState()
    {
        IdempotencyStore.Clear();
        IsKillSwitchEngaged = false;
        GetEnvironmentVariable = Environment.GetEnvironmentVariable;
    }

    public async Task<OutreachSendResult> SendOutreachAsync(OutreachSendRequest request, CancellationToken cancellationToken = default)
    {
        // 0. Emergency Kill Switch Check (T069)
        if (CheckKillSwitchActive())
        {
            var killSwitchResult = new OutreachSendResult(
                Success: false,
                MessageId: $"MSG-KILL-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
                Status: "KillSwitchEngaged",
                Detail: "Emergency kill switch is engaged. All outbound outreach is immediately halted.",
                Suppressed: true,
                WasRoutedToSink: false,
                ProviderMessageId: null
            );

            if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
            {
                IdempotencyStore[request.IdempotencyKey] = killSwitchResult;
            }

            return killSwitchResult;
        }

        // 1. Idempotency Check
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey) &&
            IdempotencyStore.TryGetValue(request.IdempotencyKey, out var existingResult))
        {
            return existingResult;
        }

        // 2. Validate recipient
        if (string.IsNullOrWhiteSpace(request.RecipientEmail))
        {
            return new OutreachSendResult(
                Success: false,
                MessageId: string.Empty,
                Status: "InvalidRecipient",
                Detail: "Recipient email cannot be empty.",
                Suppressed: false,
                WasRoutedToSink: false,
                ProviderMessageId: null
            );
        }

        // 3. Pre-send suppression check (T052)
        var isSuppressed = await _suppressionRepository.IsSuppressedAsync(request.RecipientEmail);
        if (isSuppressed)
        {
            var suppressedResult = new OutreachSendResult(
                Success: false,
                MessageId: $"MSG-SUP-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
                Status: "SuppressedByPolicy",
                Detail: $"Outreach aborted: Recipient '{request.RecipientEmail}' is on the suppression/unsubscribe list.",
                Suppressed: true,
                WasRoutedToSink: false,
                ProviderMessageId: null
            );

            if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
            {
                IdempotencyStore[request.IdempotencyKey] = suppressedResult;
            }

            return suppressedResult;
        }

        // 4. Sandbox sink routing (T050)
        var messageId = $"MSG-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var providerMessageId = $"prov_{Guid.NewGuid().ToString("N")}";

        if (request.IsTestMode)
        {
            var sinkAddress = !string.IsNullOrWhiteSpace(request.SinkAddressOverride)
                ? request.SinkAddressOverride
                : DefaultSinkAddress;

            if (string.IsNullOrWhiteSpace(sinkAddress))
            {
                // Fail closed if sink is missing in test mode
                return new OutreachSendResult(
                    Success: false,
                    MessageId: messageId,
                    Status: "SandboxSinkMissing",
                    Detail: "Test mode is active but no sink address was configured. Fails closed.",
                    Suppressed: false,
                    WasRoutedToSink: false,
                    ProviderMessageId: null
                );
            }

            var sinkMsg = new SinkMessage
            {
                Id = $"SINK-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
                IdempotencyKey = request.IdempotencyKey ?? string.Empty,
                OriginalRecipient = request.RecipientEmail,
                RoutedRecipient = sinkAddress,
                Subject = request.Subject,
                Body = request.Body,
                ProviderMessageId = providerMessageId,
                Mode = "SandboxSink",
                SentAtUtc = DateTime.UtcNow
            };

            SinkStore.Add(sinkMsg);

            var sinkResult = new OutreachSendResult(
                Success: true,
                MessageId: messageId,
                Status: "SentToTestSink",
                Detail: $"Test message safely intercepted and routed to sandbox sink '{sinkAddress}'. No live contact was emailed.",
                Suppressed: false,
                WasRoutedToSink: true,
                ProviderMessageId: providerMessageId
            );

            if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
            {
                IdempotencyStore[request.IdempotencyKey] = sinkResult;
            }

            return sinkResult;
        }

        // Production simulated send
        var prodResult = new OutreachSendResult(
            Success: true,
            MessageId: messageId,
            Status: "Dispatched",
            Detail: $"Message dispatched via email provider adapter to '{request.RecipientEmail}'.",
            Suppressed: false,
            WasRoutedToSink: false,
            ProviderMessageId: providerMessageId
        );

        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            IdempotencyStore[request.IdempotencyKey] = prodResult;
        }

        return prodResult;
    }

    public Task<IReadOnlyList<SinkMessage>> GetSinkMessagesAsync()
    {
        var list = SinkStore.OrderByDescending(m => m.SentAtUtc).ToList();
        return Task.FromResult<IReadOnlyList<SinkMessage>>(list);
    }

    public async Task<IReadOnlyList<SuppressionDto>> GetSuppressionsAsync(string? query = null)
    {
        var records = await _suppressionRepository.GetAllAsync(query);
        return records.Select(r => new SuppressionDto(
            r.Id,
            r.Target,
            r.Type,
            r.Reason,
            r.Source,
            r.CreatedBy,
            r.CreatedAtUtc
        )).ToList();
    }

    public async Task<SuppressionDto> AddSuppressionAsync(CreateSuppressionDto dto, string user)
    {
        var record = new SuppressionRecord
        {
            Id = $"SUP-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            Target = dto.Target.Trim().ToLowerInvariant(),
            Type = string.IsNullOrWhiteSpace(dto.Type) ? "Email" : dto.Type.Trim(),
            Reason = string.IsNullOrWhiteSpace(dto.Reason) ? "ManualSuppression" : dto.Reason.Trim(),
            Source = string.IsNullOrWhiteSpace(dto.Source) ? "AdminAction" : dto.Source.Trim(),
            CreatedBy = user,
            CreatedAtUtc = DateTime.UtcNow
        };

        var created = await _suppressionRepository.AddAsync(record);
        return new SuppressionDto(
            created.Id,
            created.Target,
            created.Type,
            created.Reason,
            created.Source,
            created.CreatedBy,
            created.CreatedAtUtc
        );
    }

    public Task<bool> RemoveSuppressionAsync(string id)
    {
        return _suppressionRepository.RemoveAsync(id);
    }
}
