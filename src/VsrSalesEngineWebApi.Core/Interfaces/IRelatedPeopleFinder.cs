using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Services.AgentRelay.People;

namespace VsrSalesEngineWebApi.Core.Interfaces;

/// <summary>
/// Finds people connected to a post: the poster, anyone named in the post, and recruiters, engineers and
/// decision-makers listed on the company's own public pages.
/// </summary>
public interface IRelatedPeopleFinder
{
    /// <param name="allowNetwork">False keeps the search to data we already hold (poster and the post's own text).</param>
    Task<RelatedPeopleResult> FindAsync(Opportunity opportunity, bool allowNetwork, CancellationToken cancellationToken = default);
}
