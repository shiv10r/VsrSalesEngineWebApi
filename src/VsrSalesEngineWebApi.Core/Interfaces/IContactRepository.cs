using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Entities;

namespace VsrSalesEngineWebApi.Core.Interfaces;

public interface IContactRepository
{
    Task<PagedResultDto<Contact>> GetPagedAsync(ContactFilterDto filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Contact>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Contact?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<Contact?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);
    Task<Contact> AddAsync(Contact contact, CancellationToken cancellationToken = default);
    Task<Contact?> UpdateAsync(Contact contact, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
}
