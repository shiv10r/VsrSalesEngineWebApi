using VsrSalesEngineWebApi.Core.DTOs;

namespace VsrSalesEngineWebApi.Core.Interfaces;

public record LocalLeadSearch(string District, string Category, int Max = 20);

/// <summary>
/// Seam for a future automated source of local leads (for example the official Google Places API once the
/// owner has a key). Nothing implements it yet: today leads arrive by CSV import or manual entry only.
/// A source must use an official API or permitted access, never scrape.
/// </summary>
public interface ILocalLeadSource
{
    string Name { get; }

    Task<IReadOnlyList<ImportLocalLeadRowDto>> FetchAsync(LocalLeadSearch search, CancellationToken cancellationToken = default);
}
