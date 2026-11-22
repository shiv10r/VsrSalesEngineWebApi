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
    private readonly IPlaceDiscoveryService _places;

    public LocalLeadsController(ILocalLeadService service, IPlaceDiscoveryService places)
    {
        _service = service;
        _places = places;
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

    [HttpGet("discover/status")]
    [ProducesResponseType(typeof(PlacesStatusDto), StatusCodes.Status200OK)]
    public IActionResult GetDiscoverStatus() => Ok(_places.GetStatus());

    /// <summary>Live Google Maps search (official Places API). Costs money per call, so guests are blocked.</summary>
    [DisallowGuest]
    [HttpPost("discover")]
    [ProducesResponseType(typeof(DiscoverPlacesResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Discover([FromBody] DiscoverPlacesRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _places.SearchAsync(request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (PlacesException ex)
        {
            return PlacesProblem(ex);
        }
    }

    /// <summary>Saves only name, district, category, placeId and mapsUrl of a search result.</summary>
    [DisallowGuest]
    [HttpPost("from-place")]
    [ProducesResponseType(typeof(SavePlaceResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(SavePlaceResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> FromPlace([FromBody] SavePlaceRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _places.SaveAsync(request, Actor(), cancellationToken);
            return result.AlreadySaved ? Ok(result) : Created($"/api/local-leads/{result.Lead.Id}", result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [DisallowGuest]
    [HttpGet("{id}/live-details")]
    [ProducesResponseType(typeof(PlaceLiveDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetLiveDetails(string id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _places.GetLiveDetailsAsync(id, cancellationToken));
        }
        catch (PlacesException ex)
        {
            return PlacesProblem(ex);
        }
    }

    private IActionResult PlacesProblem(PlacesException ex) => ex.Failure switch
    {
        PlacesFailure.NotConfigured => StatusCode(StatusCodes.Status503ServiceUnavailable, new
        {
            configured = false,
            message = "Google Maps search is not set up. Add GooglePlaces__ApiKey on the server."
        }),
        PlacesFailure.DailyCapReached or PlacesFailure.QuotaExceeded =>
            StatusCode(StatusCodes.Status429TooManyRequests, new { configured = true, message = ex.Message }),
        PlacesFailure.NotFound => NotFound(new { message = ex.Message }),
        _ => StatusCode(StatusCodes.Status502BadGateway, new { configured = true, message = ex.Message })
    };

    private string Actor() => User.FindFirstValue(ClaimTypes.Name) ?? "Sales Member";
}
