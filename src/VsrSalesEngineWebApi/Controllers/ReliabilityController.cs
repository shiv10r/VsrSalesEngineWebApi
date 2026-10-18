using Microsoft.AspNetCore.Mvc;
using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReliabilityController : ControllerBase
{
    private readonly IReliabilityService _reliabilityService;

    public ReliabilityController(IReliabilityService reliabilityService)
    {
        _reliabilityService = reliabilityService;
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
}
