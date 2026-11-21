using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Security;

namespace VsrSalesEngineWebApi.Controllers;

/// <summary>What the agents are doing and what they did: cards, run history and the activity log.</summary>
[ApiController]
[Route("api/agents")]
[Authorize]
public class AgentActivityController : ControllerBase
{
    private readonly IAgentActivityService _activity;
    private readonly IManualRunLauncher _launcher;

    public AgentActivityController(IAgentActivityService activity, IManualRunLauncher launcher)
    {
        _activity = activity;
        _launcher = launcher;
    }

    [HttpGet("overview")]
    [ProducesResponseType(typeof(AgentOverviewDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOverview() => Ok(await _activity.GetOverviewAsync());

    [HttpGet("runs")]
    [ProducesResponseType(typeof(IReadOnlyList<AgentRunDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRuns([FromQuery] int take = 20) => Ok(await _activity.GetRunsAsync(take));

    [HttpGet("events")]
    [ProducesResponseType(typeof(IReadOnlyList<AgentEventDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEvents([FromQuery] long afterId = 0, [FromQuery] int take = 100, [FromQuery] string? runId = null)
        => Ok(await _activity.GetEventsAsync(afterId, take, runId));

    [DisallowGuest]
    [HttpPost("run-now")]
    [ProducesResponseType(typeof(RunNowResultDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RunNow()
    {
        var user = User.FindFirst(ClaimTypes.Email)?.Value ?? User.Identity?.Name ?? "a user";
        var runId = await _launcher.StartAsync(user);

        if (runId == null)
        {
            return Conflict(new { message = "An agent run is already in progress. Watch it live instead." });
        }

        return Accepted(new RunNowResultDto(runId));
    }
}
