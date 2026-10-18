using VsrSalesEngineWebApi.Core.Entities;

namespace VsrSalesEngineWebApi.Core.Interfaces;

public interface ICampaignRepository
{
    Task<IReadOnlyList<Campaign>> GetAllAsync(string? status = null);
    Task<Campaign?> GetByIdAsync(string id);
    Task<Campaign> CreateAsync(Campaign campaign);
    Task<Campaign?> UpdateAsync(Campaign campaign);
    Task<bool> DeleteAsync(string id);
}
