using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SearchProfilesController : ControllerBase
{
    private readonly IDiscoveryService _discoveryService;

    public SearchProfilesController(IDiscoveryService discoveryService)
    {
        _discoveryService = discoveryService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SearchProfileDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var profiles = await _discoveryService.GetProfilesAsync(cancellationToken);
        return Ok(profiles);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(SearchProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
    {
        var profile = await _discoveryService.GetProfileByIdAsync(id, cancellationToken);
        if (profile == null) return NotFound(new { message = $"Search profile '{id}' not found." });
        return Ok(profile);
    }

    [HttpPost]
    [ProducesResponseType(typeof(SearchProfileDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateSearchProfileDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var actor = User.FindFirstValue(ClaimTypes.Name) ?? "Sales Member";
            var created = await _discoveryService.CreateProfileAsync(dto, actor, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(SearchProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateSearchProfileDto dto, CancellationToken cancellationToken)
    {
        var actor = User.FindFirstValue(ClaimTypes.Name) ?? "Sales Member";
        var updated = await _discoveryService.UpdateProfileAsync(id, dto, actor, cancellationToken);
        if (updated == null) return NotFound(new { message = $"Search profile '{id}' not found." });
        return Ok(updated);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var actor = User.FindFirstValue(ClaimTypes.Name) ?? "Sales Member";
        var success = await _discoveryService.DeleteProfileAsync(id, actor, cancellationToken);
        if (!success) return NotFound(new { message = $"Search profile '{id}' not found." });
        return NoContent();
    }

    [HttpPost("run")]
    [ProducesResponseType(typeof(DiscoveryRunResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> RunDiscovery([FromBody] RunDiscoveryRequestDto request, CancellationToken cancellationToken)
    {
        var actor = User.FindFirstValue(ClaimTypes.Name) ?? "Sales Member";
        var result = await _discoveryService.RunDiscoveryAsync(request, actor, cancellationToken);
        return Ok(result);
    }
}
