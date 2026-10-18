using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SourcesController : ControllerBase
{
    private readonly IDiscoveryService _discoveryService;

    public SourcesController(IDiscoveryService discoveryService)
    {
        _discoveryService = discoveryService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SourceDefinitionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var sources = await _discoveryService.GetSourcesAsync(cancellationToken);
        return Ok(sources);
    }

    [HttpPut("{id}/status")]
    [ProducesResponseType(typeof(SourceDefinitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(string id, [FromBody] UpdateSourceStatusDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _discoveryService.UpdateSourceStatusAsync(id, dto, cancellationToken);
            if (updated == null) return NotFound(new { message = $"Source '{id}' not found." });
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
