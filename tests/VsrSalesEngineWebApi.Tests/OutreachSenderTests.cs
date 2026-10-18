using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Services;
using VsrSalesEngineWebApi.Infrastructure.Repositories;
using Xunit;

namespace VsrSalesEngineWebApi.Tests;

[Collection("OutreachSenderStateCollection")]
public class OutreachSenderTests
{
    private readonly SuppressionRepository _suppressionRepo = new();
    private readonly OutreachSenderService _senderService;

    public OutreachSenderTests()
    {
        _senderService = new OutreachSenderService(_suppressionRepo);
    }

    [Fact]
    public async Task SendOutreachAsync_TestMode_RoutesToSandboxSink()
    {
        var request = new OutreachSendRequest(
            RecipientEmail: "lead@acmecorp.example",
            RecipientName: "Alex Acme",
            Subject: "Technical capability overview",
            Body: "Hi Alex, here is our capability summary.",
            IdempotencyKey: Guid.NewGuid().ToString("N"),
            IsTestMode: true,
            SinkAddressOverride: "dev-sink@vsrsalesengine.local"
        );

        var result = await _senderService.SendOutreachAsync(request);

        Assert.True(result.Success);
        Assert.Equal("SentToTestSink", result.Status);
        Assert.True(result.WasRoutedToSink);
        Assert.False(result.Suppressed);
        Assert.NotNull(result.ProviderMessageId);

        // Verify message was recorded in inspectable sink inbox
        var sinkMessages = await _senderService.GetSinkMessagesAsync();
        Assert.Contains(sinkMessages, m => m.OriginalRecipient == "lead@acmecorp.example" && m.RoutedRecipient == "dev-sink@vsrsalesengine.local");
    }

    [Fact]
    public async Task SendOutreachAsync_SuppressedEmail_AbortsSendAndFlagsSuppression()
    {
        // "optout@example.com" is seeded in SuppressionRepository
        var request = new OutreachSendRequest(
            RecipientEmail: "optout@example.com",
            RecipientName: "Opted Out User",
            Subject: "Follow up inquiry",
            Body: "Checking in on modernization needs.",
            IdempotencyKey: Guid.NewGuid().ToString("N"),
            IsTestMode: true
        );

        var result = await _senderService.SendOutreachAsync(request);

        Assert.False(result.Success);
        Assert.True(result.Suppressed);
        Assert.Equal("SuppressedByPolicy", result.Status);
        Assert.Contains("suppression/unsubscribe list", result.Detail);
    }

    [Fact]
    public async Task SendOutreachAsync_SuppressedDomain_AbortsSend()
    {
        // "blocked-competitor.example" domain is seeded in SuppressionRepository
        var request = new OutreachSendRequest(
            RecipientEmail: "engineer@blocked-competitor.example",
            RecipientName: "Domain User",
            Subject: "Pitch inquiry",
            Body: "Hello from VSR.",
            IdempotencyKey: Guid.NewGuid().ToString("N"),
            IsTestMode: true
        );

        var result = await _senderService.SendOutreachAsync(request);

        Assert.False(result.Success);
        Assert.True(result.Suppressed);
        Assert.Equal("SuppressedByPolicy", result.Status);
    }

    [Fact]
    public async Task SendOutreachAsync_IdempotencyKey_PreventsDuplicateDispatch()
    {
        var idempotencyKey = $"IDEM-KEY-{Guid.NewGuid():N}";
        var request = new OutreachSendRequest(
            RecipientEmail: "unique.lead@company.example",
            RecipientName: "Unique Lead",
            Subject: "Engineering capabilities",
            Body: "Introduction to our team.",
            IdempotencyKey: idempotencyKey,
            IsTestMode: true
        );

        var firstResult = await _senderService.SendOutreachAsync(request);
        var secondResult = await _senderService.SendOutreachAsync(request);

        Assert.True(firstResult.Success);
        Assert.True(secondResult.Success);
        Assert.Equal(firstResult.MessageId, secondResult.MessageId);
        Assert.Equal(firstResult.ProviderMessageId, secondResult.ProviderMessageId);
    }

    [Fact]
    public async Task AddAndRemoveSuppression_ManagesDurableList()
    {
        var uniqueEmail = $"unsub_{Guid.NewGuid():N}@suppressed.test";

        // 1. Verify not suppressed initially
        Assert.False(await _suppressionRepo.IsSuppressedAsync(uniqueEmail));

        // 2. Add suppression
        var added = await _senderService.AddSuppressionAsync(new CreateSuppressionDto(
            Target: uniqueEmail,
            Type: "Email",
            Reason: "OptOut",
            Source: "UserRequest"
        ), "Compliance Officer");

        Assert.NotNull(added);
        Assert.Equal(uniqueEmail, added.Target);
        Assert.True(await _suppressionRepo.IsSuppressedAsync(uniqueEmail));

        // 3. Remove suppression
        var removed = await _senderService.RemoveSuppressionAsync(added.Id);
        Assert.True(removed);
        Assert.False(await _suppressionRepo.IsSuppressedAsync(uniqueEmail));
    }
}
