namespace VsrSalesEngineWebApi.Core.Interfaces;

public record AiFormParseRequest(
    string FormType,
    string Prompt
);

public record AiOpportunityFormFields(
    string Company,
    string Title,
    string Source,
    string Location,
    string FitRating,
    string OpportunityType,
    string SourceUrl,
    string EvidenceSnippet,
    bool RequiresVendorDocs,
    string VendorRequirementNotes
);

public record AiContactFormFields(
    string Name,
    string Company,
    string Role,
    string Email,
    string Source,
    string ChannelBasis,
    string SourceUrl,
    string Notes
);

public record AiCampaignFormFields(
    string Name,
    string Audience,
    string ServiceProposition
);

public record AiFormParseResult(
    string FormType,
    string ProviderUsed,
    AiOpportunityFormFields? Opportunity,
    AiContactFormFields? Contact,
    AiCampaignFormFields? Campaign,
    IReadOnlyList<string> InferredFields
);

public interface IAiFormService
{
    Task<AiFormParseResult> ParseFormPromptAsync(AiFormParseRequest request, CancellationToken cancellationToken = default);
}
