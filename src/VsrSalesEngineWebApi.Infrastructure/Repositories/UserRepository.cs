using System.Collections.Concurrent;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private static readonly ConcurrentDictionary<string, User> UsersByEmail = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<Guid, User> UsersById = new();

    static UserRepository()
    {
        // Seed default developer and sales admin
        var admin = new User
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Email = "admin@vsrsalesengine.com",
            PasswordHash = "Admin123!", // Dev test password
            FullName = "VSR Admin",
            Role = "Admin",
            IsGuest = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        var teamMember = new User
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Email = "sales@vsrsalesengine.com",
            PasswordHash = "Sales123!",
            FullName = "VSR Sales Team",
            Role = "SalesMember",
            IsGuest = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        UsersByEmail[admin.Email] = admin;
        UsersById[admin.Id] = admin;

        UsersByEmail[teamMember.Email] = teamMember;
        UsersById[teamMember.Id] = teamMember;
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        UsersByEmail.TryGetValue(email, out var user);
        return Task.FromResult(user);
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        UsersById.TryGetValue(id, out var user);
        return Task.FromResult(user);
    }

    public Task<User> CreateAsync(User user, CancellationToken cancellationToken = default)
    {
        UsersByEmail[user.Email] = user;
        UsersById[user.Id] = user;
        return Task.FromResult(user);
    }

    public Task UpdateLastLoginAsync(Guid id, DateTime loginTimeUtc, CancellationToken cancellationToken = default)
    {
        if (UsersById.TryGetValue(id, out var user))
        {
            user.LastLoginAtUtc = loginTimeUtc;
        }
        return Task.CompletedTask;
    }
}

