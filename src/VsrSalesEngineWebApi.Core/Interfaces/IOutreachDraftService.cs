using VsrSalesEngineWebApi.Core.DTOs;

namespace VsrSalesEngineWebApi.Core.Interfaces;

public interface IOutreachDraftService
{
    Task<IReadOnlyList<OutreachTemplateDto>> GetTemplatesAsync(string? category = null);
    Task<OutreachTemplateDto?> GetTemplateByIdAsync(string id);
    Task<OutreachTemplateDto> CreateTemplateAsync(CreateOutreachTemplateDto dto, string user);
    Task<OutreachTemplateDto?> UpdateTemplateAsync(string id, UpdateOutreachTemplateDto dto);
    Task<bool> DeleteTemplateAsync(string id);

    Task<DraftGenerationResult> GenerateDraftAsync(DraftGenerationRequest request);
    DraftValidationResult ValidateDraft(DraftContentValidationRequest request);
}
