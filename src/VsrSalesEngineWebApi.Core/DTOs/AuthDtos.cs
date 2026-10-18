namespace VsrSalesEngineWebApi.Core.DTOs;

public record LoginRequestDto(string Email, string Password);

public record AuthResponseDto(
    bool Success,
    string? Token,
    string? ErrorMessage,
    UserDto? User
);

public record UserDto(
    Guid Id,
    string Email,
    string FullName,
    string Role,
    bool IsGuest
);

public record GuestStatusDto(
    bool AllowedInCurrentEnvironment,
    bool ServerFlagEnabled,
    string EnvironmentName,
    string Notice
);

