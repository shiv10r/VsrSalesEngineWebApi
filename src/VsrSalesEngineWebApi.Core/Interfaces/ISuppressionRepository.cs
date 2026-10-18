using VsrSalesEngineWebApi.Core.Entities;

namespace VsrSalesEngineWebApi.Core.Interfaces;

public interface ISuppressionRepository
{
    Task<IReadOnlyList<SuppressionRecord>> GetAllAsync(string? query = null);
    Task<SuppressionRecord?> GetByIdAsync(string id);
    Task<bool> IsSuppressedAsync(string email);
    Task<SuppressionRecord> AddAsync(SuppressionRecord record);
    Task<bool> RemoveAsync(string id);
}
