using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ContactsController : ControllerBase
{
    private readonly IContactService _contactService;

    public ContactsController(IContactService contactService)
    {
        _contactService = contactService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<ContactDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] string? query,
        [FromQuery] string? company,
        [FromQuery] string? status,
        [FromQuery] string? qualificationStatus,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var filter = new ContactFilterDto(query, company, status, qualificationStatus, page, pageSize);
        var result = await _contactService.GetPagedContactsAsync(filter, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ContactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
    {
        var contact = await _contactService.GetContactByIdAsync(id, cancellationToken);
        if (contact == null) return NotFound(new { message = $"Contact '{id}' not found." });
        return Ok(contact);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ContactDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateContactDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var actor = User.FindFirstValue(ClaimTypes.Name) ?? "Sales Member";
            var created = await _contactService.CreateContactAsync(dto, actor, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ContactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateContactDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var actor = User.FindFirstValue(ClaimTypes.Name) ?? "Sales Member";
            var updated = await _contactService.UpdateContactAsync(id, dto, actor, cancellationToken);
            if (updated == null) return NotFound(new { message = $"Contact '{id}' not found." });
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("{id}/qualify")]
    [ProducesResponseType(typeof(ContactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Qualify(string id, [FromBody] QualifyContactDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var actor = User.FindFirstValue(ClaimTypes.Name) ?? "Sales Member";
            var updated = await _contactService.QualifyContactAsync(id, dto, actor, cancellationToken);
            if (updated == null) return NotFound(new { message = $"Contact '{id}' not found." });
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
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var actor = User.FindFirstValue(ClaimTypes.Name) ?? "Sales Member";
        var success = await _contactService.DeleteContactAsync(id, actor, cancellationToken);
        if (!success) return NotFound(new { message = $"Contact '{id}' not found." });
        return NoContent();
    }
}
