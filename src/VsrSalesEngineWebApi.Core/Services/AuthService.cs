using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Core.Security;

namespace VsrSalesEngineWebApi.Core.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly JwtOptions _jwtOptions;
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public AuthService(
        IUserRepository userRepository,
        IOptions<JwtOptions> jwtOptions,
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _jwtOptions = jwtOptions.Value;
        _environment = environment;
        _configuration = configuration;
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return new AuthResponseDto(false, null, "Email and password are required.", null);
        }

        var user = await _userRepository.GetByEmailAsync(request.Email.Trim().ToLowerInvariant(), cancellationToken);
        if (user == null)
        {
            return new AuthResponseDto(false, null, "Invalid email or credentials.", null);
        }

        // Verify password
        if (!VerifyPassword(request.Password, user.PasswordHash))
        {
            return new AuthResponseDto(false, null, "Invalid email or credentials.", null);
        }

        await _userRepository.UpdateLastLoginAsync(user.Id, DateTime.UtcNow, cancellationToken);

        var token = GenerateJwtToken(user);
        var userDto = new UserDto(user.Id, user.Email, user.FullName, user.Role, user.IsGuest);

        return new AuthResponseDto(true, token, null, userDto);
    }

    public async Task<AuthResponseDto> CreateGuestSessionAsync(CancellationToken cancellationToken = default)
    {
        var status = GetGuestStatus();
        if (!status.AllowedInCurrentEnvironment || !status.ServerFlagEnabled)
        {
            return new AuthResponseDto(
                false,
                null,
                "Guest mode bypass is disabled in this environment or disallowed by server policy. Production requires valid JWT credentials.",
                null
            );
        }

        // Return isolated guest identity with restricted permissions
        var guestUser = new User
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Email = "guest.dev@vsrsalesengine.local",
            FullName = "Local Development Guest",
            Role = "Guest",
            IsGuest = true,
            CreatedAtUtc = DateTime.UtcNow,
            LastLoginAtUtc = DateTime.UtcNow
        };

        var token = GenerateJwtToken(guestUser);
        var userDto = new UserDto(guestUser.Id, guestUser.Email, guestUser.FullName, guestUser.Role, true);

        return await Task.FromResult(new AuthResponseDto(true, token, null, userDto));
    }

    public GuestStatusDto GetGuestStatus()
    {
        var isDevelopment = _environment.IsDevelopment();
        var allowGuestFlag = _configuration.GetValue<bool>("Features:AllowDevelopmentGuest", false);

        string notice;
        if (!isDevelopment)
        {
            notice = "Guest mode is strictly disabled in production builds. JWT sign-in is required.";
        }
        else if (!allowGuestFlag)
        {
            notice = "Development environment detected, but Features:AllowDevelopmentGuest is false. Guest bypass is blocked.";
        }
        else
        {
            notice = "Local development guest mode is available with simulated sample data and restricted privileges.";
        }

        return new GuestStatusDto(
            AllowedInCurrentEnvironment: isDevelopment,
            ServerFlagEnabled: allowGuestFlag,
            EnvironmentName: _environment.EnvironmentName,
            Notice: notice
        );
    }

    public async Task<UserDto?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Parse("00000000-0000-0000-0000-000000000001"))
        {
            return new UserDto(userId, "guest.dev@vsrsalesengine.local", "Local Development Guest", "Guest", true);
        }

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null) return null;

        return new UserDto(user.Id, user.Email, user.FullName, user.Role, user.IsGuest);
    }

    private string GenerateJwtToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, user.Role),
            new("is_guest", user.IsGuest.ToString().ToLowerInvariant())
        };

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtOptions.ExpirationMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static bool VerifyPassword(string inputPassword, string storedHash)
    {
        // For development/starter, allow direct test seed match or SHA256 comparison
        if (storedHash == inputPassword) return true;

        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(inputPassword));
        var hex = Convert.ToHexString(bytes).ToLowerInvariant();
        return string.Equals(hex, storedHash, StringComparison.OrdinalIgnoreCase);
    }
}

