using VsrSalesEngineWebApi.Core.Entities;

namespace VsrSalesEngineWebApi.Core.Interfaces;

public interface IOutreachTemplateRepository
{
    Task<IReadOnlyList<OutreachTemplate>> GetAllAsync(string? category = null);
    Task<OutreachTemplate?> GetByIdAsync(string id);
    Task<OutreachTemplate> CreateAsync(OutreachTemplate template);
    Task<OutreachTemplate?> UpdateAsync(OutreachTemplate template);
    Task<bool> DeleteAsync(string id);
}
