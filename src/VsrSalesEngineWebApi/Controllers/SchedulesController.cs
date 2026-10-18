using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SchedulesController : ControllerBase
{
    private readonly IScheduleExecutionService _scheduleService;

    public SchedulesController(IScheduleExecutionService scheduleService)
    {
        _scheduleService = scheduleService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AutomationScheduleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var schedules = await _scheduleService.GetSchedulesAsync();
        return Ok(schedules);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(AutomationScheduleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id)
    {
        var schedule = await _scheduleService.GetScheduleByIdAsync(id);
        if (schedule == null) return NotFound(new { message = $"Schedule '{id}' not found." });
        return Ok(schedule);
    }

    [HttpPost]
    [ProducesResponseType(typeof(AutomationScheduleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateScheduleDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Description))
        {
            return BadRequest(new { message = "Name and Description are required." });
        }

        var actor = User.FindFirstValue(ClaimTypes.Name) ?? "Sales Member";
        var created = await _scheduleService.CreateScheduleAsync(dto, actor);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(AutomationScheduleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateScheduleDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Description))
        {
            return BadRequest(new { message = "Name and Description cannot be empty." });
        }

        var updated = await _scheduleService.UpdateScheduleAsync(id, dto);
        if (updated == null) return NotFound(new { message = $"Schedule '{id}' not found." });
        return Ok(updated);
    }

    [HttpPatch("{id}/toggle")]
    [ProducesResponseType(typeof(AutomationScheduleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Toggle(string id, [FromBody] ToggleScheduleDto dto)
    {
        var updated = await _scheduleService.ToggleScheduleAsync(id, dto.IsEnabled);
        if (updated == null) return NotFound(new { message = $"Schedule '{id}' not found." });
        return Ok(updated);
    }

    [HttpPost("{id}/run-now")]
    [ProducesResponseType(typeof(ScheduleRunExecutionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RunNow(string id)
    {
        var actor = User.FindFirstValue(ClaimTypes.Name) ?? "Manual Trigger";
        var result = await _scheduleService.ExecuteScheduleNowAsync(id, actor);
        if (result.Status == "ScheduleNotFound")
        {
            return NotFound(new { message = result.Message });
        }
        return Ok(result);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id)
    {
        var deleted = await _scheduleService.DeleteScheduleAsync(id);
        if (!deleted) return NotFound(new { message = $"Schedule '{id}' not found." });
        return NoContent();
    }
}
