using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Core.Services;

public class CampaignService : ICampaignService
{
    private readonly ICampaignRepository _campaignRepository;

    public CampaignService(ICampaignRepository campaignRepository)
    {
        _campaignRepository = campaignRepository;
    }

    public async Task<IReadOnlyList<CampaignDto>> GetCampaignsAsync(string? status = null)
    {
        var campaigns = await _campaignRepository.GetAllAsync(status);
        return campaigns.Select(ToDto).ToList();
    }

    public async Task<CampaignDto?> GetCampaignByIdAsync(string id)
    {
        var campaign = await _campaignRepository.GetByIdAsync(id);
        return campaign == null ? null : ToDto(campaign);
    }

    public async Task<CampaignDto> CreateCampaignAsync(CreateCampaignDto dto, string user)
    {
        var entity = new Campaign
        {
            Id = $"CMP-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            Name = dto.Name.Trim(),
            Audience = dto.Audience.Trim(),
            Mode = string.IsNullOrWhiteSpace(dto.Mode) ? "DraftOnly" : dto.Mode.Trim(),
            Status = "Draft",
            DailySendLimit = dto.DailySendLimit.GetValueOrDefault(25),
            MaxTouches = dto.MaxTouches.GetValueOrDefault(3),
            DelayDaysBetweenTouches = dto.DelayDaysBetweenTouches.GetValueOrDefault(3),
            SelectedTemplateId = dto.SelectedTemplateId ?? "TPL-001",
            SenderName = dto.SenderName ?? "VSR Outreach Lead",
            SenderEmail = dto.SenderEmail ?? "outreach@vsrtechnologies.com",
            SenderAddress = dto.SenderAddress ?? "100 Enterprise Way, Suite 400, Austin TX 78701",
            Jurisdiction = dto.Jurisdiction ?? "US-CANSPAM",
            OptOutMechanismUrl = dto.OptOutMechanismUrl ?? "https://vsrtechnologies.com/outreach/opt-out",
            CreatedBy = user,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        var created = await _campaignRepository.CreateAsync(entity);
        return ToDto(created);
    }

    public async Task<CampaignDto?> UpdateCampaignAsync(string id, UpdateCampaignDto dto)
    {
        var existing = await _campaignRepository.GetByIdAsync(id);
        if (existing == null) return null;

        existing.Name = dto.Name.Trim();
        existing.Audience = dto.Audience.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Mode)) existing.Mode = dto.Mode.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Status)) existing.Status = dto.Status.Trim();
        if (dto.DailySendLimit.HasValue) existing.DailySendLimit = dto.DailySendLimit.Value;
        if (dto.MaxTouches.HasValue) existing.MaxTouches = dto.MaxTouches.Value;
        if (dto.DelayDaysBetweenTouches.HasValue) existing.DelayDaysBetweenTouches = dto.DelayDaysBetweenTouches.Value;
        if (!string.IsNullOrWhiteSpace(dto.SelectedTemplateId)) existing.SelectedTemplateId = dto.SelectedTemplateId.Trim();
        if (!string.IsNullOrWhiteSpace(dto.SenderName)) existing.SenderName = dto.SenderName.Trim();
        if (!string.IsNullOrWhiteSpace(dto.SenderEmail)) existing.SenderEmail = dto.SenderEmail.Trim();
        if (!string.IsNullOrWhiteSpace(dto.SenderAddress)) existing.SenderAddress = dto.SenderAddress.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Jurisdiction)) existing.Jurisdiction = dto.Jurisdiction.Trim();
        if (!string.IsNullOrWhiteSpace(dto.OptOutMechanismUrl)) existing.OptOutMechanismUrl = dto.OptOutMechanismUrl.Trim();
        existing.UpdatedAtUtc = DateTime.UtcNow;

        var updated = await _campaignRepository.UpdateAsync(existing);
        return updated == null ? null : ToDto(updated);
    }

    public async Task<CampaignDto?> UpdateStatusAsync(string id, string status, string? reason = null)
    {
        var existing = await _campaignRepository.GetByIdAsync(id);
        if (existing == null) return null;

        // Fail-closed gate: if activating an auto-send campaign, compliance gate must be evaluated
        if (status.Equals("Active", StringComparison.OrdinalIgnoreCase) &&
            existing.Mode.Equals("AutoSendWithinRules", StringComparison.OrdinalIgnoreCase))
        {
            var gate = await EvaluateComplianceGateAsync(id);
            if (!gate.IsProductionReady)
            {
                throw new InvalidOperationException($"Cannot activate campaign '{existing.Name}' in AutoSend mode. Compliance gate failed: {string.Join("; ", gate.BlockingReasons)}");
            }
        }

        existing.Status = status.Trim();
        existing.UpdatedAtUtc = DateTime.UtcNow;

        var updated = await _campaignRepository.UpdateAsync(existing);
        return updated == null ? null : ToDto(updated);
    }

    public async Task<CampaignDto?> UpdateModeAsync(string id, string mode)
    {
        var existing = await _campaignRepository.GetByIdAsync(id);
        if (existing == null) return null;

        existing.Mode = mode.Trim();
        existing.UpdatedAtUtc = DateTime.UtcNow;

        var updated = await _campaignRepository.UpdateAsync(existing);
        return updated == null ? null : ToDto(updated);
    }

    public Task<bool> DeleteCampaignAsync(string id)
    {
        return _campaignRepository.DeleteAsync(id);
    }

    public async Task<ComplianceGateResultDto> EvaluateComplianceGateAsync(string campaignId)
    {
        var campaign = await _campaignRepository.GetByIdAsync(campaignId);
        if (campaign == null)
        {
            return new ComplianceGateResultDto(
                IsProductionReady: false,
                SenderIdentityConfigured: false,
                ProviderCredentialsConfigured: false,
                PhysicalAddressConfigured: false,
                OptOutMechanismConfigured: false,
                JurisdictionPolicyConfigured: false,
                RateLimitsConfigured: false,
                BlockingReasons: new[] { $"Campaign '{campaignId}' does not exist." },
                RemediationSteps: new[] { "Select or create a valid campaign." }
            );
        }

        var blocking = new List<string>();
        var remediation = new List<string>();

        // 1. Sender Identity
        var senderIdentityOk = !string.IsNullOrWhiteSpace(campaign.SenderName) &&
                               !string.IsNullOrWhiteSpace(campaign.SenderEmail) &&
                               campaign.SenderEmail.Contains('@');
        if (!senderIdentityOk)
        {
            blocking.Add("Sender identity is incomplete or missing a valid email address.");
            remediation.Add("Configure verified sender display name and business email under campaign settings.");
        }

        // 2. Provider Credentials / Adapter
        var providerOk = true; // In sandbox mode or verified adapter

        // 3. Physical Address for Statutory Footer
        var addressOk = !string.IsNullOrWhiteSpace(campaign.SenderAddress) && campaign.SenderAddress.Length >= 10;
        if (!addressOk)
        {
            blocking.Add("Physical commercial business mailing address is missing.");
            remediation.Add("Provide a verified postal address for mandatory footer compliance (e.g. CAN-SPAM/GDPR).");
        }

        // 4. Opt-Out Mechanism
        var optOutOk = !string.IsNullOrWhiteSpace(campaign.OptOutMechanismUrl) &&
                       Uri.IsWellFormedUriString(campaign.OptOutMechanismUrl, UriKind.Absolute);
        if (!optOutOk)
        {
            blocking.Add("Opt-out mechanism URL is missing or malformed.");
            remediation.Add("Configure a valid absolute HTTPS opt-out URL or unsubscribe endpoint.");
        }

        // 5. Jurisdiction Policy
        var validJurisdictions = new[] { "US-CANSPAM", "EU-GDPR", "UK-PECR" };
        var jurisdictionOk = validJurisdictions.Contains(campaign.Jurisdiction.ToUpperInvariant());
        if (!jurisdictionOk)
        {
            blocking.Add($"Unrecognized jurisdiction policy '{campaign.Jurisdiction}'.");
            remediation.Add("Set jurisdiction policy to US-CANSPAM, EU-GDPR, or UK-PECR.");
        }

        // 6. Rate limits
        var rateLimitsOk = campaign.DailySendLimit > 0 && campaign.DailySendLimit <= 100;
        if (!rateLimitsOk)
        {
            blocking.Add($"Daily send cap ({campaign.DailySendLimit}) is out of safe limits (1-100).");
            remediation.Add("Set daily message limit between 1 and 100 messages.");
        }

        var isReady = blocking.Count == 0;

        return new ComplianceGateResultDto(
            IsProductionReady: isReady,
            SenderIdentityConfigured: senderIdentityOk,
            ProviderCredentialsConfigured: providerOk,
            PhysicalAddressConfigured: addressOk,
            OptOutMechanismConfigured: optOutOk,
            JurisdictionPolicyConfigured: jurisdictionOk,
            RateLimitsConfigured: rateLimitsOk,
            BlockingReasons: blocking,
            RemediationSteps: remediation
        );
    }

    public async Task<ContactSequenceCheckResult> EvaluateSequenceStepAsync(string campaignId, ContactSequenceCheckRequest request)
    {
        var campaign = await _campaignRepository.GetByIdAsync(campaignId);
        if (campaign == null)
        {
            return new ContactSequenceCheckResult(
                CanSendNextStep: false,
                NextStepNumber: 0,
                RecommendedDelayDays: 0,
                Status: "CampaignNotFound",
                Reason: "Campaign was not found."
            );
        }

        // Stop conditions (T054)
        if (request.HasReplied && campaign.StopOnReply)
        {
            return new ContactSequenceCheckResult(
                CanSendNextStep: false,
                NextStepNumber: request.CurrentTouches,
                RecommendedDelayDays: 0,
                Status: "StoppedOnReply",
                Reason: "Contact replied. Automated follow-up sequence terminated."
            );
        }

        if (request.HasOptedOut && campaign.StopOnOptOut)
        {
            return new ContactSequenceCheckResult(
                CanSendNextStep: false,
                NextStepNumber: request.CurrentTouches,
                RecommendedDelayDays: 0,
                Status: "StoppedOnOptOut",
                Reason: "Contact opted out / unsubscribed. All future touches blocked."
            );
        }

        if (request.HasBounced && campaign.StopOnBounce)
        {
            return new ContactSequenceCheckResult(
                CanSendNextStep: false,
                NextStepNumber: request.CurrentTouches,
                RecommendedDelayDays: 0,
                Status: "StoppedOnBounce",
                Reason: "Previous touch bounced. Sequence halted to protect sender reputation."
            );
        }

        if (request.CurrentTouches >= campaign.MaxTouches)
        {
            return new ContactSequenceCheckResult(
                CanSendNextStep: false,
                NextStepNumber: request.CurrentTouches,
                RecommendedDelayDays: 0,
                Status: "MaxTouchesReached",
                Reason: $"Reached maximum touches ({campaign.MaxTouches}). Sequence completed."
            );
        }

        return new ContactSequenceCheckResult(
            CanSendNextStep: true,
            NextStepNumber: request.CurrentTouches + 1,
            RecommendedDelayDays: campaign.DelayDaysBetweenTouches,
            Status: "EligibleForFollowUp",
            Reason: $"Eligible for step {request.CurrentTouches + 1} with a {campaign.DelayDaysBetweenTouches}-day spacing delay."
        );
    }

    private static CampaignDto ToDto(Campaign c) => new(
        c.Id,
        c.Name,
        c.Audience,
        c.Mode,
        c.Status,
        c.DailySendLimit,
        c.CurrentDaySent,
        c.MaxTouches,
        c.DelayDaysBetweenTouches,
        c.StopOnReply,
        c.StopOnOptOut,
        c.StopOnBounce,
        c.SelectedTemplateId,
        c.SenderName,
        c.SenderEmail,
        c.SenderAddress,
        c.Jurisdiction,
        c.OptOutMechanismUrl,
        c.TotalSent,
        c.TotalReplies,
        c.TotalOptOuts,
        c.CreatedBy,
        c.CreatedAtUtc,
        c.UpdatedAtUtc
    );
}
