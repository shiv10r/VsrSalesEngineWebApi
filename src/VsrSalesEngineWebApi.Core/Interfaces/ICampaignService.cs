using VsrSalesEngineWebApi.Core.DTOs;

namespace VsrSalesEngineWebApi.Core.Interfaces;

public interface ICampaignService
{
    Task<IReadOnlyList<CampaignDto>> GetCampaignsAsync(string? status = null);
    Task<CampaignDto?> GetCampaignByIdAsync(string id);
    Task<CampaignDto> CreateCampaignAsync(CreateCampaignDto dto, string user);
    Task<CampaignDto?> UpdateCampaignAsync(string id, UpdateCampaignDto dto);
    Task<CampaignDto?> UpdateStatusAsync(string id, string status, string? reason = null);
    Task<CampaignDto?> UpdateModeAsync(string id, string mode);
    Task<bool> DeleteCampaignAsync(string id);

    Task<ComplianceGateResultDto> EvaluateComplianceGateAsync(string campaignId);
    Task<ContactSequenceCheckResult> EvaluateSequenceStepAsync(string campaignId, ContactSequenceCheckRequest request);
}
