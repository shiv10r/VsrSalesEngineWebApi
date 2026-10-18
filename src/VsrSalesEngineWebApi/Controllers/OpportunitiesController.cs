using System.Security.Claims;
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
    [ProducesResponseType(typeof(PagedResultDto<OpportunityDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] string? query,
        [FromQuery] string? status,
        [FromQuery] string? source,
        [FromQuery] string? fit,
        [FromQuery] bool? hasVendorDocs,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var filter = new OpportunityFilterDto(query, status, source, fit, hasVendorDocs, page, pageSize);
        var pagedResults = await _opportunityService.GetPagedOpportunitiesAsync(filter, cancellationToken);
        return Ok(pagedResults);
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
            var actor = User.FindFirstValue(ClaimTypes.Name) ?? "Sales Member";
            var created = await _opportunityService.CreateOpportunityAsync(dto, actor, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(OpportunityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateOpportunityDto dto, CancellationToken cancellationToken)
    {
        var actor = User.FindFirstValue(ClaimTypes.Name) ?? "Sales Member";
        var updated = await _opportunityService.UpdateOpportunityAsync(id, dto, actor, cancellationToken);
        if (updated == null) return NotFound(new { message = $"Opportunity '{id}' not found." });
        return Ok(updated);
    }

    [HttpPatch("{id}/status")]
    [ProducesResponseType(typeof(OpportunityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TransitionStatus(string id, [FromBody] TransitionStatusDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var actor = User.FindFirstValue(ClaimTypes.Name) ?? "Sales Member";
            var updated = await _opportunityService.TransitionStatusAsync(id, dto, actor, cancellationToken);
            if (updated == null) return NotFound(new { message = $"Opportunity '{id}' not found." });
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(string id, CancellationToken cancellationToken)
    {
        var actor = User.FindFirstValue(ClaimTypes.Name) ?? "Sales Member";
        var success = await _opportunityService.ArchiveOpportunityAsync(id, actor, cancellationToken);
        if (!success) return NotFound(new { message = $"Opportunity '{id}' not found." });
        return NoContent();
    }
}
