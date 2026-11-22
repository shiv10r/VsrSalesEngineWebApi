using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Services.AgentRelay;

namespace VsrSalesEngineWebApi.Core.Interfaces;

public interface IAgentSettingsRepository
{
    Task<AgentSettings?> GetAsync();
    Task SaveAsync(AgentSettings settings);
}

public record SettingsSaveResult(AgentSettingsDto? Settings, IReadOnlyList<ValidationErrorDto> Errors)
{
    public bool Ok => Settings != null;
}

public interface IAgentSettingsService
{
    Task<AgentSettingsDto> GetAsync();

    /// <summary>Validates and saves. Nothing is changed when validation fails.</summary>
    Task<SettingsSaveResult> SaveAsync(UpdateAgentSettingsDto dto, string user);

    /// <summary>What a run needs: current names, which agents are off.</summary>
    Task<AgentRuntimeSettings> GetRuntimeAsync();
}
