using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OutreachController : ControllerBase
{
    private readonly IOutreachDraftService _draftService;

    public OutreachController(IOutreachDraftService draftService)
    {
        _draftService = draftService;
    }

    [HttpGet("templates")]
    [ProducesResponseType(typeof(IReadOnlyList<OutreachTemplateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTemplates([FromQuery] string? category)
    {
        var templates = await _draftService.GetTemplatesAsync(category);
        return Ok(templates);
    }

    [HttpGet("templates/{id}")]
    [ProducesResponseType(typeof(OutreachTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTemplateById(string id)
    {
        var template = await _draftService.GetTemplateByIdAsync(id);
        if (template == null) return NotFound(new { message = $"Template '{id}' not found." });
        return Ok(template);
    }

    [HttpPost("templates")]
    [ProducesResponseType(typeof(OutreachTemplateDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateTemplate([FromBody] CreateOutreachTemplateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.SubjectTemplate) || string.IsNullOrWhiteSpace(dto.BodyTemplate))
        {
            return BadRequest(new { message = "Name, SubjectTemplate, and BodyTemplate are required." });
        }

        var actor = User.FindFirstValue(ClaimTypes.Name) ?? "Sales Member";
        var created = await _draftService.CreateTemplateAsync(dto, actor);
        return CreatedAtAction(nameof(GetTemplateById), new { id = created.Id }, created);
    }

    [HttpPut("templates/{id}")]
    [ProducesResponseType(typeof(OutreachTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateTemplate(string id, [FromBody] UpdateOutreachTemplateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.SubjectTemplate) || string.IsNullOrWhiteSpace(dto.BodyTemplate))
        {
            return BadRequest(new { message = "Name, SubjectTemplate, and BodyTemplate cannot be empty." });
        }

        var updated = await _draftService.UpdateTemplateAsync(id, dto);
        if (updated == null) return NotFound(new { message = $"Template '{id}' not found." });
        return Ok(updated);
    }

    [HttpDelete("templates/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTemplate(string id)
    {
        var deleted = await _draftService.DeleteTemplateAsync(id);
        if (!deleted) return NotFound(new { message = $"Template '{id}' not found." });
        return NoContent();
    }

    [HttpPost("drafts/generate")]
    [ProducesResponseType(typeof(DraftGenerationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateDraft([FromBody] DraftGenerationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ContactName) || string.IsNullOrWhiteSpace(request.CompanyName))
        {
            return BadRequest(new { message = "ContactName and CompanyName are required for draft generation." });
        }

        var result = await _draftService.GenerateDraftAsync(request);
        return Ok(result);
    }

    [HttpPost("drafts/validate")]
    [ProducesResponseType(typeof(DraftValidationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult ValidateDraft([FromBody] DraftContentValidationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.Body))
        {
            return BadRequest(new { message = "Subject and Body must not be empty." });
        }

        var result = _draftService.ValidateDraft(request);
        return Ok(result);
    }
}
