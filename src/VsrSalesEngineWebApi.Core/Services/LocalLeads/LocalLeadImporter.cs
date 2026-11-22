using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Core.Services.LocalLeads;

/// <summary>Validates, de-duplicates and saves a batch of rows from a CSV import.</summary>
public class LocalLeadImporter
{
    public const int MaxRows = 2000;
    public const string DefaultLabel = "CSV import";

    private readonly ILocalLeadRepository _repository;

    public LocalLeadImporter(ILocalLeadRepository repository)
    {
        _repository = repository;
    }

    public async Task<ImportLocalLeadsResultDto> ImportAsync(
        ImportLocalLeadsRequestDto request, string actor, CancellationToken cancellationToken)
    {
        var rows = request.Rows;
        if (rows is null || rows.Count == 0) throw new ArgumentException("rows is required and cannot be empty.");
        if (rows.Count > MaxRows) throw new ArgumentException($"Too many rows: {rows.Count}. The limit is {MaxRows} per import.");

        var label = string.IsNullOrWhiteSpace(request.SourceLabel) ? DefaultLabel : request.SourceLabel.Trim();
        if (label.Length > 100) label = label[..100];

        var (nameKeys, phones) = await _repository.GetDuplicateKeysAsync(cancellationToken);
        var accepted = new List<LocalLead>();
        var errors = new List<ImportRowErrorDto>();
        var duplicates = 0;

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var input = new LeadInput(row.BusinessName, row.District, row.Category, row.Address, row.Phone,
                row.Website, row.Rating, row.ReviewCount, null);

            var (lead, error) = LocalLeadBuilder.Build(input, "CSV import", label, actor);
            if (lead is null)
            {
                errors.Add(new ImportRowErrorDto(i + 1, error!));
                continue;
            }

            var key = LocalLeadBuilder.DuplicateKey(lead.NormalizedName, lead.District);
            var phoneSeen = lead.NormalizedPhone.Length > 0 && phones.Contains(lead.NormalizedPhone);
            if (nameKeys.Contains(key) || phoneSeen)
            {
                duplicates++;
                continue;
            }

            nameKeys.Add(key);
            if (lead.NormalizedPhone.Length > 0) phones.Add(lead.NormalizedPhone);
            accepted.Add(lead);
        }

        if (accepted.Count > 0) await _repository.AddRangeAsync(accepted, cancellationToken);

        return new ImportLocalLeadsResultDto(accepted.Count, duplicates, errors.Count, errors);
    }
}
