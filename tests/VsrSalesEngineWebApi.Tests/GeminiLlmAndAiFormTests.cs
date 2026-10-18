using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Core.Services;
using Xunit;

namespace VsrSalesEngineWebApi.Tests;

public class GeminiLlmAndAiFormTests
{
    [Fact]
    public async Task GeminiLlmProvider_ReturnsGroundedSynthesis_WhenApiKeyEmpty()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "LLM:Gemini:Model", "gemini-2.5-flash" },
                { "LLM:Gemini:ApiKey", "" }
            })
            .Build();

        var httpClient = new HttpClient();
        var provider = new GeminiLlmProvider(httpClient, config, NullLogger<GeminiLlmProvider>.Instance);

        Assert.Equal("Gemini", provider.ProviderName);
        Assert.Equal("gemini-2.5-flash", provider.DefaultModel);

        var request = new LLMCompletionRequest(
            Prompt: "Draft an introductory message for Northstar Digital regarding .NET modernization."
        );

        var result = await provider.CompleteAsync(request);

        Assert.True(result.Success);
        Assert.Equal("Gemini", result.Provider);
        Assert.Contains("gemini-2.5-flash", result.Model);
        Assert.False(string.IsNullOrWhiteSpace(result.Content));
    }

    [Fact]
    public async Task LlmRouter_RoutesToGeminiByDefault()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "LLM:DefaultProvider", "Gemini" }
            })
            .Build();

        var geminiProvider = new GeminiLlmProvider(
            new HttpClient(),
            config,
            NullLogger<GeminiLlmProvider>.Instance
        );

        var router = new LlmRouter(
            new[] { geminiProvider },
            config,
            NullLogger<LlmRouter>.Instance
        );

        Assert.Equal("Gemini", router.DefaultProviderName);

        var request = new LLMCompletionRequest(Prompt: "Evaluate lead qualification");
        var result = await router.RouteCompletionAsync(request);

        Assert.True(result.Success);
        Assert.Equal("Gemini", result.Provider);
    }

    [Fact]
    public async Task AiFormService_ParsesOpportunityPrompt_HeuristicFallback()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "LLM:DefaultProvider", "Gemini" }
            })
            .Build();

        var gemini = new GeminiLlmProvider(new HttpClient(), config, NullLogger<GeminiLlmProvider>.Instance);
        var router = new LlmRouter(new[] { gemini }, config, NullLogger<LlmRouter>.Instance);
        var aiFormService = new AiFormService(router, NullLogger<AiFormService>.Instance);

        var request = new AiFormParseRequest(
            FormType: "opportunity",
            Prompt: "Senior .NET Lead at Microsoft in Bengaluru with high fit rating, requires ISO vendor registration"
        );

        var result = await aiFormService.ParseFormPromptAsync(request);

        Assert.Equal("opportunity", result.FormType);
        Assert.NotNull(result.Opportunity);
        Assert.Equal("Microsoft", result.Opportunity.Company);
        Assert.Contains(".NET", result.Opportunity.Title);
        Assert.Equal("Bengaluru, India", result.Opportunity.Location);
        Assert.True(result.Opportunity.RequiresVendorDocs);
    }

    [Fact]
    public async Task AiFormService_ParsesContactPrompt_HeuristicFallback()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "LLM:DefaultProvider", "Gemini" }
            })
            .Build();

        var gemini = new GeminiLlmProvider(new HttpClient(), config, NullLogger<GeminiLlmProvider>.Instance);
        var router = new LlmRouter(new[] { gemini }, config, NullLogger<LlmRouter>.Instance);
        var aiFormService = new AiFormService(router, NullLogger<AiFormService>.Instance);

        var request = new AiFormParseRequest(
            FormType: "contact",
            Prompt: "Maya Patel, Engineering Manager at Northstar Digital, maya.patel@northstar.com"
        );

        var result = await aiFormService.ParseFormPromptAsync(request);

        Assert.Equal("contact", result.FormType);
        Assert.NotNull(result.Contact);
        Assert.Equal("Maya Patel", result.Contact.Name);
        Assert.Equal("Northstar Digital", result.Contact.Company);
        Assert.Equal("Engineering Manager", result.Contact.Role);
        Assert.Equal("maya.patel@northstar.com", result.Contact.Email);
    }
}
