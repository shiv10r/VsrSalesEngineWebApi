using System.Security.Cryptography;
using System.Text;

namespace VsrSalesEngineWebApi.Core.Security;

/// <summary>
/// Salted PBKDF2 (SHA-256) password hashing using only the built-in framework.
/// Stored format: PBKDF2-SHA256$iterations$saltBase64$hashBase64. Anything else (including plain text
/// and unsalted hex digests) is never accepted.
/// </summary>
public static class PasswordHasher
{
    public const int Iterations = 100_000;
    private const string Prefix = "PBKDF2-SHA256";
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    private const int MaxAcceptedIterations = 2_000_000; // stored value is data: do not let it become a CPU burner

    public static string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Derive(password, salt, Iterations, HashBytes);
        return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string? stored)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(stored)) return false;
        if (!TryParse(stored, out var iterations, out var salt, out var expected)) return false;

        var actual = Derive(password, salt, iterations, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    /// <summary>True when the stored value is in the expected hashed format (not plain text).</summary>
    public static bool IsHashed(string? stored) => stored != null && TryParse(stored, out _, out _, out _);

    private static byte[] Derive(string password, byte[] salt, int iterations, int length) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, length);

    private static bool TryParse(string stored, out int iterations, out byte[] salt, out byte[] hash)
    {
        iterations = 0;
        salt = Array.Empty<byte>();
        hash = Array.Empty<byte>();

        var parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != Prefix) return false;
        if (!int.TryParse(parts[1], out iterations) || iterations < Iterations || iterations > MaxAcceptedIterations) return false;

        try
        {
            salt = Convert.FromBase64String(parts[2]);
            hash = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        return salt.Length >= SaltBytes && hash.Length >= HashBytes;
    }
}
