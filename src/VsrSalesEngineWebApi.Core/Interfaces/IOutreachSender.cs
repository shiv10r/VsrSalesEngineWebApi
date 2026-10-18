using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Entities;

namespace VsrSalesEngineWebApi.Core.Interfaces;

public interface IOutreachSender
{
    Task<OutreachSendResult> SendOutreachAsync(OutreachSendRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SinkMessage>> GetSinkMessagesAsync();
    Task<IReadOnlyList<SuppressionDto>> GetSuppressionsAsync(string? query = null);
    Task<SuppressionDto> AddSuppressionAsync(CreateSuppressionDto dto, string user);
    Task<bool> RemoveSuppressionAsync(string id);
}
