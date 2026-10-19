using VsrSalesEngineWebApi.Core.Entities;

namespace VsrSalesEngineWebApi.Core.Interfaces;

public interface ILiveJobFeedService
{
    Task<IReadOnlyList<Opportunity>> FetchHackerNewsContractJobsAsync(string? keywords = null, int maxResults = 15, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Opportunity>> FetchWeWorkRemotelyContractJobsAsync(string? keywords = null, int maxResults = 15, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Opportunity>> FetchRemotiveContractJobsAsync(string? keywords = null, int maxResults = 15, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Opportunity>> FetchJobicyContractJobsAsync(string? keywords = null, int maxResults = 15, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Opportunity>> FetchAllLiveContractJobsAsync(string? keywords = null, IEnumerable<string>? sources = null, int maxResults = 30, CancellationToken cancellationToken = default);
}
