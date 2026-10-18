using Microsoft.AspNetCore.Mvc;
using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Core.Services;

namespace VsrSalesEngineWebApi.Controllers;

public record ToggleKillSwitchRequest(bool? Engaged);

[ApiController]
[Route("api/[controller]")]
public class ReliabilityController : ControllerBase
{
    private readonly IReliabilityService _reliabilityService;
    private readonly IHostEnvironment _env;

    public ReliabilityController(
        IReliabilityService reliabilityService,
        IHostEnvironment env)
    {
        _reliabilityService = reliabilityService;
        _env = env;
    }

    [HttpGet("metrics")]
    public async Task<ActionResult<ReliabilityMetricsDto>> GetMetrics()
    {
        var metrics = await _reliabilityService.GetMetricsAsync();
        return Ok(metrics);
    }

    [HttpGet("failures")]
    public async Task<ActionResult<IReadOnlyList<ExecutionRecordDto>>> GetFailures()
    {
        var failures = await _reliabilityService.GetFailuresAsync();
        return Ok(failures);
    }

    [HttpGet("dead-letters")]
    public async Task<ActionResult<IReadOnlyList<ExecutionRecordDto>>> GetDeadLetters()
    {
        var deadLetters = await _reliabilityService.GetDeadLettersAsync();
        return Ok(deadLetters);
    }

    [HttpPost("failures/{id}/retry")]
    public async Task<ActionResult<RetryExecutionResultDto>> RetryFailure(string id)
    {
        var result = await _reliabilityService.RetryAsync(id, HttpContext.RequestAborted);
        if (!result.Success && result.Status == "NotFound")
        {
            return NotFound(result);
        }

        if (!result.Success && result.Status == "Ineligible")
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("simulate-failure")]
    public async Task<ActionResult<ExecutionRecordDto>> SimulateFailure([FromBody] SimulateFailureRequestDto request)
    {
        var record = await _reliabilityService.SimulateFailureAsync(
            request.FailureType,
            request.JobType,
            request.ReferenceId
        );
        return Ok(record);
    }

    [HttpGet("kill-switch")]
    public ActionResult<object> GetKillSwitchStatus()
    {
        return Ok(new
        {
            isEngaged = OutreachSenderService.CheckKillSwitchActive(),
            mode = Environment.GetEnvironmentVariable("OUTREACH_MODE") ?? "Test",
            timestampUtc = DateTime.UtcNow
        });
    }

    [HttpPost("kill-switch/toggle")]
    public ActionResult<object> ToggleKillSwitch([FromBody] ToggleKillSwitchRequest? request)
    {
        var targetState = request?.Engaged ?? !OutreachSenderService.CheckKillSwitchActive();
        OutreachSenderService.IsKillSwitchEngaged = targetState;
        return Ok(new
        {
            isEngaged = OutreachSenderService.CheckKillSwitchActive(),
            action = targetState ? "KillSwitchEngaged" : "KillSwitchDisengaged",
            message = targetState ? "Operational kill switch engaged. Outbound sending halted." : "Operational kill switch disengaged. Outbound operations resumed.",
            timestampUtc = DateTime.UtcNow
        });
    }

    [HttpGet("smoke-test")]
    public async Task<ActionResult<OperationalSmokeDrillResultDto>> RunOperationalSmokeDrill()
    {
        var result = await _reliabilityService.RunSmokeDrillAsync();
        return Ok(result);
    }
}
