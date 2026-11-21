namespace VsrSalesEngineWebApi.Infrastructure.Data;

/// <summary>Which users the seeder may create. The default is none.</summary>
public sealed record UserSeedOptions(bool DemoUsers, string? AdminEmail = null, string? AdminPassword = null)
{
    public const int MinAdminPasswordLength = 12;

    public static UserSeedOptions None { get; } = new(false);
    public static UserSeedOptions Demo { get; } = new(true);

    /// <summary>
    /// Development: the two demo users. Anywhere else: no users, unless Seed:AdminEmail and
    /// Seed:AdminPassword are both configured, which creates one Admin. A half-set or weak
    /// configuration stops startup instead of silently leaving a weak login.
    /// </summary>
    public static UserSeedOptions Resolve(bool isDevelopment, string? adminEmail, string? adminPassword)
    {
        if (isDevelopment) return Demo;

        var hasEmail = !string.IsNullOrWhiteSpace(adminEmail);
        var hasPassword = !string.IsNullOrWhiteSpace(adminPassword);
        if (!hasEmail && !hasPassword) return None;

        if (hasEmail != hasPassword)
        {
            throw new InvalidOperationException("Seed:AdminEmail and Seed:AdminPassword must be set together (or both left empty).");
        }

        if (!adminEmail!.Contains('@'))
        {
            throw new InvalidOperationException("Seed:AdminEmail must be an email address.");
        }

        if (adminPassword!.Length < MinAdminPasswordLength)
        {
            throw new InvalidOperationException($"Seed:AdminPassword must be at least {MinAdminPasswordLength} characters.");
        }

        return new UserSeedOptions(false, adminEmail.Trim().ToLowerInvariant(), adminPassword);
    }
}
