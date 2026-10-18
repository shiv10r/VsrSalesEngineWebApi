using System.Net.Mail;
using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Core.Services;

public class ContactService : IContactService
{
    private readonly IContactRepository _contactRepository;

    private static readonly HashSet<string> RolePrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "info", "support", "billing", "sales", "contact", "admin", "jobs", "careers", "press", "inquiries", "help"
    };

    public ContactService(IContactRepository contactRepository)
    {
        _contactRepository = contactRepository;
    }

    public async Task<PagedResultDto<ContactDto>> GetPagedContactsAsync(ContactFilterDto filter, CancellationToken cancellationToken = default)
    {
        var paged = await _contactRepository.GetPagedAsync(filter, cancellationToken);
        var items = paged.Items.Select(MapContact).ToList();
        return new PagedResultDto<ContactDto>(items, paged.TotalCount, paged.Page, paged.PageSize, paged.TotalPages);
    }

    public async Task<ContactDto?> GetContactByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var contact = await _contactRepository.GetByIdAsync(id, cancellationToken);
        return contact == null ? null : MapContact(contact);
    }

    public async Task<ContactDto> CreateContactAsync(CreateContactDto dto, string actor, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) throw new ArgumentException("Contact name cannot be empty.");
        if (string.IsNullOrWhiteSpace(dto.Email)) throw new ArgumentException("Contact email cannot be empty.");

        var normalizedEmail = NormalizeEmail(dto.Email);
        if (!IsValidEmailSyntax(normalizedEmail))
        {
            throw new ArgumentException($"Invalid email format '{dto.Email}'.");
        }

        // Deduplication: check if already exists
        var existing = await _contactRepository.GetByNormalizedEmailAsync(normalizedEmail, cancellationToken);
        if (existing != null)
        {
            return MapContact(existing);
        }

        var isRole = CheckIsRoleAccount(normalizedEmail);

        var contact = new Contact
        {
            Id = $"CNT-{DateTime.UtcNow.Ticks % 10000:D4}",
            Name = dto.Name.Trim(),
            Company = string.IsNullOrWhiteSpace(dto.Company) ? "Unassigned" : dto.Company.Trim(),
            Role = string.IsNullOrWhiteSpace(dto.Role) ? "Unspecified" : dto.Role.Trim(),
            Email = dto.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            Source = dto.Source ?? "Manual entry",
            SourceUrl = dto.SourceUrl?.Trim() ?? string.Empty,
            ChannelBasis = dto.ChannelBasis ?? "ExplicitBusinessContact",
            Status = "Not contacted",
            QualificationStatus = isRole ? "BlockedByPolicy" : "Unverified",
            IsRoleAccount = isRole,
            Notes = dto.Notes?.Trim(),
            CreatedBy = actor,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        var created = await _contactRepository.AddAsync(contact, cancellationToken);
        return MapContact(created);
    }

    public async Task<ContactDto?> UpdateContactAsync(string id, UpdateContactDto dto, string actor, CancellationToken cancellationToken = default)
    {
        var existing = await _contactRepository.GetByIdAsync(id, cancellationToken);
        if (existing == null) return null;

        if (!string.IsNullOrWhiteSpace(dto.Name)) existing.Name = dto.Name.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Company)) existing.Company = dto.Company.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Role)) existing.Role = dto.Role.Trim();
        if (!string.IsNullOrWhiteSpace(dto.SourceUrl)) existing.SourceUrl = dto.SourceUrl.Trim();
        if (dto.Notes != null) existing.Notes = dto.Notes.Trim();

        if (!string.IsNullOrWhiteSpace(dto.Email) && !dto.Email.Equals(existing.Email, StringComparison.OrdinalIgnoreCase))
        {
            var normalized = NormalizeEmail(dto.Email);
            if (!IsValidEmailSyntax(normalized))
            {
                throw new ArgumentException($"Invalid email format '{dto.Email}'.");
            }
            existing.Email = dto.Email.Trim();
            existing.NormalizedEmail = normalized;
            existing.IsRoleAccount = CheckIsRoleAccount(normalized);
            if (existing.IsRoleAccount)
            {
                existing.QualificationStatus = "BlockedByPolicy";
            }
        }

        existing.UpdatedAtUtc = DateTime.UtcNow;
        var updated = await _contactRepository.UpdateAsync(existing, cancellationToken);
        return updated == null ? null : MapContact(updated);
    }

    public async Task<ContactDto?> QualifyContactAsync(string id, QualifyContactDto dto, string actor, CancellationToken cancellationToken = default)
    {
        var existing = await _contactRepository.GetByIdAsync(id, cancellationToken);
        if (existing == null) return null;

        var validQualifications = new[] { "Unverified", "Verified", "BlockedByPolicy", "OptedOut", "Qualified" };
        if (!validQualifications.Contains(dto.QualificationStatus, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Invalid qualification status '{dto.QualificationStatus}'. Allowed: {string.Join(", ", validQualifications)}");
        }

        existing.QualificationStatus = dto.QualificationStatus;
        if (!string.IsNullOrWhiteSpace(dto.Status)) existing.Status = dto.Status.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Notes))
        {
            existing.Notes = string.IsNullOrWhiteSpace(existing.Notes)
                ? $"[{DateTime.UtcNow:s}] {dto.Notes.Trim()}"
                : $"{existing.Notes} | [{DateTime.UtcNow:s}] {dto.Notes.Trim()}";
        }
        existing.UpdatedAtUtc = DateTime.UtcNow;

        var updated = await _contactRepository.UpdateAsync(existing, cancellationToken);
        return updated == null ? null : MapContact(updated);
    }

    public async Task<bool> DeleteContactAsync(string id, string actor, CancellationToken cancellationToken = default)
    {
        return await _contactRepository.DeleteAsync(id, cancellationToken);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static bool IsValidEmailSyntax(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        try
        {
            var addr = new MailAddress(email);
            return addr.Address == email && email.Contains('.') && !email.EndsWith('.');
        }
        catch
        {
            return false;
        }
    }

    private static bool CheckIsRoleAccount(string email)
    {
        var atIndex = email.IndexOf('@');
        if (atIndex <= 0) return false;
        var userPart = email.Substring(0, atIndex);
        return RolePrefixes.Contains(userPart);
    }

    private static ContactDto MapContact(Contact c) => new(
        c.Id,
        c.Name,
        c.Company,
        c.Role,
        c.Email,
        c.Source,
        c.SourceUrl,
        c.ChannelBasis,
        c.Status,
        c.QualificationStatus,
        c.IsRoleAccount,
        c.Notes,
        c.CreatedBy,
        c.CreatedAtUtc,
        c.UpdatedAtUtc
    );
}
