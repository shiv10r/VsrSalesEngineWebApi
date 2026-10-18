using VsrSalesEngineWebApi.Core.DTOs;

namespace VsrSalesEngineWebApi.Core.Interfaces;

public interface IContactService
{
    Task<PagedResultDto<ContactDto>> GetPagedContactsAsync(ContactFilterDto filter, CancellationToken cancellationToken = default);
    Task<ContactDto?> GetContactByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<ContactDto> CreateContactAsync(CreateContactDto dto, string actor, CancellationToken cancellationToken = default);
    Task<ContactDto?> UpdateContactAsync(string id, UpdateContactDto dto, string actor, CancellationToken cancellationToken = default);
    Task<ContactDto?> QualifyContactAsync(string id, QualifyContactDto dto, string actor, CancellationToken cancellationToken = default);
    Task<bool> DeleteContactAsync(string id, string actor, CancellationToken cancellationToken = default);
}
