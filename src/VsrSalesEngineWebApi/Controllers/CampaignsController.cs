using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CampaignsController : ControllerBase
{
    private readonly ICampaignService _campaignService;

    public CampaignsController(ICampaignService campaignService)
    {
        _campaignService = campaignService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CampaignDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] string? status)
    {
        var campaigns = await _campaignService.GetCampaignsAsync(status);
        return Ok(campaigns);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(CampaignDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id)
    {
        var campaign = await _campaignService.GetCampaignByIdAsync(id);
        if (campaign == null) return NotFound(new { message = $"Campaign '{id}' not found." });
        return Ok(campaign);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CampaignDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateCampaignDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Audience))
        {
            return BadRequest(new { message = "Name and Audience are required." });
        }

        var actor = User.FindFirstValue(ClaimTypes.Name) ?? "Sales Member";
        var created = await _campaignService.CreateCampaignAsync(dto, actor);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(CampaignDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateCampaignDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Audience))
        {
            return BadRequest(new { message = "Name and Audience cannot be empty." });
        }

        var updated = await _campaignService.UpdateCampaignAsync(id, dto);
        if (updated == null) return NotFound(new { message = $"Campaign '{id}' not found." });
        return Ok(updated);
    }

    [HttpPatch("{id}/status")]
    [ProducesResponseType(typeof(CampaignDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateStatus(string id, [FromBody] UpdateCampaignStatusDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Status))
        {
            return BadRequest(new { message = "Status cannot be empty." });
        }

        try
        {
            var updated = await _campaignService.UpdateStatusAsync(id, dto.Status, dto.Reason);
            if (updated == null) return NotFound(new { message = $"Campaign '{id}' not found." });
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("{id}/mode")]
    [ProducesResponseType(typeof(CampaignDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMode(string id, [FromBody] UpdateCampaignModeDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Mode))
        {
            return BadRequest(new { message = "Mode cannot be empty." });
        }

        var updated = await _campaignService.UpdateModeAsync(id, dto.Mode);
        if (updated == null) return NotFound(new { message = $"Campaign '{id}' not found." });
        return Ok(updated);
    }

    [HttpGet("{id}/compliance-gate")]
    [ProducesResponseType(typeof(ComplianceGateResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetComplianceGate(string id)
    {
        var result = await _campaignService.EvaluateComplianceGateAsync(id);
        return Ok(result);
    }

    [HttpPost("{id}/sequence-check")]
    [ProducesResponseType(typeof(ContactSequenceCheckResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckSequenceStep(string id, [FromBody] ContactSequenceCheckRequest request)
    {
        var result = await _campaignService.EvaluateSequenceStepAsync(id, request);
        return Ok(result);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id)
    {
        var deleted = await _campaignService.DeleteCampaignAsync(id);
        if (!deleted) return NotFound(new { message = $"Campaign '{id}' not found." });
        return NoContent();
    }
}
