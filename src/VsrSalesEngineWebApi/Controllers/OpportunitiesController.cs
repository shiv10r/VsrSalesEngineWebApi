using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // Requires valid JWT or development guest session
public class OpportunitiesController : ControllerBase
{
    private readonly IOpportunityService _opportunityService;

    public OpportunitiesController(IOpportunityService opportunityService)
    {
        _opportunityService = opportunityService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<OpportunityDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] string? query, [FromQuery] string? status, [FromQuery] string? source, CancellationToken cancellationToken)
    {
        var filter = new OpportunityFilterDto(query, status, source);
        var results = await _opportunityService.GetOpportunitiesAsync(filter, cancellationToken);
        return Ok(results);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(OpportunityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
    {
        var opp = await _opportunityService.GetOpportunityByIdAsync(id, cancellationToken);
        if (opp == null) return NotFound(new { message = $"Opportunity '{id}' not found." });
        return Ok(opp);
    }

    [HttpPost]
    [ProducesResponseType(typeof(OpportunityDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateOpportunityDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var created = await _opportunityService.CreateOpportunityAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("{id}/status")]
    [ProducesResponseType(typeof(OpportunityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(string id, [FromBody] UpdateStatusRequest request, CancellationToken cancellationToken)
    {
        var updated = await _opportunityService.UpdateStatusAsync(id, request.Status, cancellationToken);
        if (updated == null) return NotFound(new { message = $"Opportunity '{id}' not found." });
        return Ok(updated);
    }
}

public record UpdateStatusRequest(string Status);

