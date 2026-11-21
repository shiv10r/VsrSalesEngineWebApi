using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Controllers;

[ApiController]
[Route("api/agents")]
public class AgentsController : ControllerBase
{
    private const string KeyHeader = "X-Agent-Key";
    private readonly IDueScheduleRunner _runner;
    private readonly string _runKey;

    public AgentsController(IDueScheduleRunner runner, IConfiguration configuration)
    {
        _runner = runner;
        _runKey = configuration["Agents:RunKey"] ?? string.Empty;
    }

    /// <summary>
    /// Called by an external timer (GitHub Actions) to wake the server and run due agents.
    /// Protected by a shared secret; disabled until Agents:RunKey is configured.
    /// </summary>
    [HttpPost("run-due")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(DueRunSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> RunDue(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_runKey))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "Agent run endpoint is disabled: Agents:RunKey is not configured." });
        }

        if (!Request.Headers.TryGetValue(KeyHeader, out var supplied) || !KeyMatches(supplied.ToString()))
        {
            return Unauthorized(new { message = "Invalid agent key." });
        }

        var summary = await _runner.RunDueAsync("ExternalTimer", cancellationToken);
        return Ok(summary);
    }

    [HttpGet("status")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<AgentStatusDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus()
    {
        return Ok(await _runner.GetStatusAsync());
    }

    private bool KeyMatches(string supplied)
    {
        var expected = SHA256.HashData(Encoding.UTF8.GetBytes(_runKey));
        var actual = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
