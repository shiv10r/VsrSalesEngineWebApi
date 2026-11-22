using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Security;

namespace VsrSalesEngineWebApi.Controllers;

/// <summary>What the owner can change about the agents: names, on/off, keywords and shifts.</summary>
[ApiController]
[Route("api/agents/settings")]
[Authorize]
public class AgentSettingsController : ControllerBase
{
    private readonly IAgentSettingsService _settings;

    public AgentSettingsController(IAgentSettingsService settings)
    {
        _settings = settings;
    }

    [HttpGet]
    [ProducesResponseType(typeof(AgentSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get() => Ok(await _settings.GetAsync());

    [DisallowGuest]
    [HttpPut]
    [ProducesResponseType(typeof(AgentSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SettingsErrorsDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Put([FromBody] UpdateAgentSettingsDto dto)
    {
        var user = User.FindFirst(ClaimTypes.Email)?.Value ?? User.Identity?.Name ?? "a user";
        var result = await _settings.SaveAsync(dto, user);
        return result.Ok ? Ok(result.Settings) : BadRequest(new SettingsErrorsDto(result.Errors));
    }
}
