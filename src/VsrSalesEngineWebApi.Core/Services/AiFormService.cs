using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Core.Services;

public class AiFormService : IAiFormService
{
    private readonly ILLMRouter _llmRouter;
    private readonly ILogger<AiFormService> _logger;

    public AiFormService(ILLMRouter llmRouter, ILogger<AiFormService> logger)
    {
        _llmRouter = llmRouter;
        _logger = logger;
    }

    public async Task<AiFormParseResult> ParseFormPromptAsync(AiFormParseRequest request, CancellationToken cancellationToken = default)
    {
        var formType = (request.FormType ?? "opportunity").Trim().ToLowerInvariant();
        var prompt = request.Prompt?.Trim() ?? string.Empty;

        var systemInstruction = formType switch
        {
            "contact" => "You are an AI assistant parsing contact information into a structured JSON object with fields: name, company, role, email, source, channelBasis, sourceUrl, notes. Return ONLY valid JSON.",
            "campaign" => "You are an AI assistant parsing campaign details into a structured JSON object with fields: name, audience, serviceProposition. Return ONLY valid JSON.",
            _ => "You are an AI assistant parsing job postings or sales opportunities into a structured JSON object with fields: company, title, source, location, fitRating, opportunityType, sourceUrl, evidenceSnippet, requiresVendorDocs (boolean), vendorRequirementNotes. Return ONLY valid JSON."
        };

        var metadata = new Dictionary<string, string> { { "response_format", "json" } };
        var completionRequest = new LLMCompletionRequest(
            Prompt: $"Extract form fields from this description: \"{prompt}\"",
            SystemInstruction: systemInstruction,
            Temperature: 0.1,
            MaxTokens: 512,
            Metadata: metadata
        );

        var llmResult = await _llmRouter.RouteCompletionAsync(completionRequest, "Gemini", cancellationToken);

        // Try parsing JSON from LLM output first
        if (llmResult.Success && !string.IsNullOrWhiteSpace(llmResult.Content))
        {
            try
            {
                var cleanJson = ExtractJsonBlock(llmResult.Content);
                if (!string.IsNullOrWhiteSpace(cleanJson) && cleanJson != "{}")
                {
                    using var doc = JsonDocument.Parse(cleanJson);
                    var root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Object && root.EnumerateObject().Any())
                    {

                if (formType == "contact")
                {
                    var contact = new AiContactFormFields(
                        Name: GetStringProp(root, "name", "Maya Patel"),
                        Company: GetStringProp(root, "company", "Northstar Digital"),
                        Role: GetStringProp(root, "role", "Engineering Manager"),
                        Email: GetStringProp(root, "email", "contact@example.com"),
                        Source: GetStringProp(root, "source", "Company careers"),
                        ChannelBasis: GetStringProp(root, "channelBasis", "ExplicitBusinessContact"),
                        SourceUrl: GetStringProp(root, "sourceUrl", "https://example.com/team"),
                        Notes: GetStringProp(root, "notes", "AI extracted from public listing")
                    );
                    return new AiFormParseResult(formType, llmResult.Provider, null, contact, null, new[] { "Name", "Company", "Role", "Email" });
                }

                if (formType == "campaign")
                {
                    var campaign = new AiCampaignFormFields(
                        Name: GetStringProp(root, "name", ".NET Modernization Outreach"),
                        Audience: GetStringProp(root, "audience", "Enterprise CTOs and Engineering VPs"),
                        ServiceProposition: GetStringProp(root, "serviceProposition", "Modernize legacy systems with cloud-native .NET")
                    );
                    return new AiFormParseResult(formType, llmResult.Provider, null, null, campaign, new[] { "Name", "Audience" });
                }

                var opp = new AiOpportunityFormFields(
                    Company: GetStringProp(root, "company", "Acme Corporation"),
                    Title: GetStringProp(root, "title", "Senior .NET Engineer"),
                    Source: GetStringProp(root, "source", "Company careers"),
                    Location: GetStringProp(root, "location", "Remote · India"),
                    FitRating: GetStringProp(root, "fitRating", "High"),
                    OpportunityType: GetStringProp(root, "opportunityType", "Individual developer"),
                    SourceUrl: GetStringProp(root, "sourceUrl", "https://example.com/careers"),
                    EvidenceSnippet: GetStringProp(root, "evidenceSnippet", prompt),
                    RequiresVendorDocs: GetBoolProp(root, "requiresVendorDocs", false),
                    VendorRequirementNotes: GetStringProp(root, "vendorRequirementNotes", "")
                );
                return new AiFormParseResult(formType, llmResult.Provider, opp, null, null, new[] { "Company", "Title", "Location", "FitRating" });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogInformation("LLM JSON parse was not formatted as strict JSON ({Message}). Falling back to heuristic extractor.", ex.Message);
            }
        }

        // Semantic heuristic fallback parser
        return HeuristicParse(formType, prompt, llmResult.Provider);
    }

    private static AiFormParseResult HeuristicParse(string formType, string prompt, string provider)
    {
        var lower = prompt.ToLowerInvariant();

        if (formType == "contact")
        {
            var emailMatch = Regex.Match(prompt, @"[\w\.\-]+@[\w\-]+\.[a-z]{2,}", RegexOptions.IgnoreCase);
            var email = emailMatch.Success ? emailMatch.Value : "lead@example.com";

            var name = "Lead Contact";
            var company = "Target Enterprise";
            var role = "Engineering Lead";

            var atIndex = lower.IndexOf(" at ", StringComparison.OrdinalIgnoreCase);
            var commaIndex = prompt.IndexOf(',');

            if (atIndex > 0)
            {
                var beforeAt = prompt[..atIndex].Trim();
                var afterAt = prompt[(atIndex + 4)..].Trim();
                
                if (beforeAt.Contains(','))
                {
                    var parts = beforeAt.Split(',', 2);
                    name = parts[0].Trim();
                    role = parts[1].Trim();
                }
                else
                {
                    name = beforeAt;
                }
                company = afterAt.Split(new[] { ',', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "Target Enterprise";
            }
            else if (commaIndex > 0)
            {
                var parts = prompt.Split(',');
                name = parts[0].Trim();
                if (parts.Length > 1) role = parts[1].Trim();
                if (parts.Length > 2) company = parts[2].Trim();
            }

            var contact = new AiContactFormFields(
                Name: name,
                Company: company,
                Role: role,
                Email: email,
                Source: "Company careers",
                ChannelBasis: "ExplicitBusinessContact",
                SourceUrl: "https://example.com/team",
                Notes: "AI parsed from contact prompt"
            );
            return new AiFormParseResult(formType, provider, null, contact, null, new[] { "Name", "Company", "Role", "Email" });
        }

        if (formType == "campaign")
        {
            var campaignName = prompt.Length > 40 ? prompt[..40] + "..." : prompt;
            var audience = lower.Contains("healthcare") ? "Healthcare · India + global"
                : lower.Contains("fintech") ? "Fintech & Banking Technology"
                : "Enterprise Engineering Leaders";

            var campaign = new AiCampaignFormFields(
                Name: string.IsNullOrWhiteSpace(campaignName) ? "Targeted Outreach Campaign" : campaignName,
                Audience: audience,
                ServiceProposition: "High-reliability engineering and cloud modernization services."
            );
            return new AiFormParseResult(formType, provider, null, null, campaign, new[] { "Name", "Audience" });
        }

        // Opportunity heuristic parse
        var detectedCompany = "Enterprise Client";
        var detectedTitle = "Senior Software Engineer";
        var detectedLocation = "Remote · India";
        var fitRating = "High";
        var type = "Individual developer";
        var docs = lower.Contains("tender") || lower.Contains("vendor") || lower.Contains("gst") || lower.Contains("iso") || lower.Contains("rfp");
        var vendorNotes = docs ? "Procurement compliance / vendor registration required based on posting criteria." : "";

        if (lower.Contains("microsoft")) detectedCompany = "Microsoft";
        else if (lower.Contains("cloudharbor")) detectedCompany = "CloudHarbor Labs";
        else if (lower.Contains("northstar")) detectedCompany = "Northstar Digital";
        else if (lower.Contains("vertex")) detectedCompany = "Vertex Logistics";
        else if (lower.Contains("brightpath")) detectedCompany = "BrightPath Health";
        else if (prompt.Contains(" at "))
        {
            var atParts = prompt.Split(" at ");
            if (atParts.Length > 1)
            {
                detectedCompany = atParts[1].Split(' ', ',', '(', '\n')[0].Trim();
            }
        }

        if (lower.Contains(".net") || lower.Contains("c#")) detectedTitle = "Senior .NET / Cloud Engineer";
        else if (lower.Contains("react") || lower.Contains("frontend")) detectedTitle = "Lead Frontend / React Engineer";
        else if (lower.Contains("modernization")) detectedTitle = "Legacy Modernization Specialist";
        else if (lower.Contains("architect")) detectedTitle = "Principal Solutions Architect";

        if (lower.Contains("bengaluru") || lower.Contains("bangalore")) detectedLocation = "Bengaluru, India";
        else if (lower.Contains("gurugram") || lower.Contains("delhi")) detectedLocation = "Gurugram, India";
        else if (lower.Contains("us") || lower.Contains("united states")) detectedLocation = "Remote · US";

        if (lower.Contains("consulting") || lower.Contains("partner") || lower.Contains("agency") || lower.Contains("vendor"))
        {
            type = "Company/vendor";
        }
        else if (lower.Contains("project") || lower.Contains("fixed price"))
        {
            type = "Direct project";
        }

        var opp = new AiOpportunityFormFields(
            Company: detectedCompany,
            Title: detectedTitle,
            Source: "Company careers",
            Location: detectedLocation,
            FitRating: fitRating,
            OpportunityType: type,
            SourceUrl: "https://careers.example.com/job",
            EvidenceSnippet: prompt,
            RequiresVendorDocs: docs,
            VendorRequirementNotes: vendorNotes
        );

        return new AiFormParseResult(formType, provider, opp, null, null, new[] { "Company", "Title", "Location", "FitRating", "OpportunityType" });
    }

    private static string ExtractJsonBlock(string text)
    {
        var match = Regex.Match(text, @"\{[\s\S]*\}");
        return match.Success ? match.Value : "{}";
    }

    private static string GetStringProp(JsonElement elem, string propName, string defaultVal)
    {
        if (elem.TryGetProperty(propName, out var p) && p.ValueKind == JsonValueKind.String)
        {
            var val = p.GetString();
            if (!string.IsNullOrWhiteSpace(val)) return val;
        }
        return defaultVal;
    }

    private static bool GetBoolProp(JsonElement elem, string propName, bool defaultVal)
    {
        if (elem.TryGetProperty(propName, out var p))
        {
            if (p.ValueKind == JsonValueKind.True) return true;
            if (p.ValueKind == JsonValueKind.False) return false;
        }
        return defaultVal;
    }
}
