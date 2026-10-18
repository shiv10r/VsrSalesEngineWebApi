using VsrSalesEngineWebApi.Core.Entities;

namespace VsrSalesEngineWebApi.Core.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User> CreateAsync(User user, CancellationToken cancellationToken = default);
    Task UpdateLastLoginAsync(Guid id, DateTime loginTimeUtc, CancellationToken cancellationToken = default);
}

