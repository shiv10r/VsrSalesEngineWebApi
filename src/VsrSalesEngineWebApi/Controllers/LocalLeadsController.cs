using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Security;

namespace VsrSalesEngineWebApi.Controllers;

[ApiController]
[Route("api/local-leads")]
[Authorize]
public class LocalLeadsController : ControllerBase
{
    private readonly ILocalLeadService _service;

    public LocalLeadsController(ILocalLeadService service)
    {
        _service = service;
    }

    [HttpGet("options")]
    [ProducesResponseType(typeof(LocalLeadOptionsDto), StatusCodes.Status200OK)]
    public IActionResult GetOptions() => Ok(_service.GetOptions());

    [HttpGet]
    [ProducesResponseType(typeof(LocalLeadPageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] string? district,
        [FromQuery] string? category,
        [FromQuery] string? status,
        [FromQuery] bool? hasWebsite,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var filter = new LocalLeadFilterDto(district, category, status, hasWebsite, q, page, pageSize);
        return Ok(await _service.GetPagedAsync(filter, cancellationToken));
    }

    [HttpGet("summary")]
    [ProducesResponseType(typeof(LocalLeadSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken) =>
        Ok(await _service.GetSummaryAsync(cancellationToken));

    [DisallowGuest]
    [HttpPost]
    [ProducesResponseType(typeof(LocalLeadDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateLocalLeadDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var created = await _service.CreateAsync(dto, Actor(), cancellationToken);
            return Created($"/api/local-leads/{created.Id}", created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [DisallowGuest]
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(LocalLeadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateLocalLeadDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _service.UpdateAsync(id, dto, cancellationToken);
            if (updated is null) return NotFound(new { message = $"Local lead '{id}' not found." });
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [DisallowGuest]
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var deleted = await _service.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound(new { message = $"Local lead '{id}' not found." });
    }

    [DisallowGuest]
    [HttpPost("import")]
    [ProducesResponseType(typeof(ImportLocalLeadsResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Import([FromBody] ImportLocalLeadsRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.ImportAsync(request, Actor(), cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private string Actor() => User.FindFirstValue(ClaimTypes.Name) ?? "Sales Member";
}
