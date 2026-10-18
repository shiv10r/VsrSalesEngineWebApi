using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Core.Services;

public class LlmRouter : ILLMRouter
{
    private readonly IEnumerable<ILLMProvider> _providers;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LlmRouter> _logger;

    public string DefaultProviderName => _configuration["LLM:DefaultProvider"] ?? "Gemini";

    public LlmRouter(
        IEnumerable<ILLMProvider> providers,
        IConfiguration configuration,
        ILogger<LlmRouter> logger)
    {
        _providers = providers;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<LLMCompletionResult> RouteCompletionAsync(
        LLMCompletionRequest request,
        string? preferredProvider = null,
        CancellationToken cancellationToken = default)
    {
        var targetProviderName = !string.IsNullOrWhiteSpace(preferredProvider)
            ? preferredProvider
            : DefaultProviderName;

        var primary = _providers.FirstOrDefault(p => string.Equals(p.ProviderName, targetProviderName, StringComparison.OrdinalIgnoreCase))
                      ?? _providers.FirstOrDefault(p => string.Equals(p.ProviderName, "Gemini", StringComparison.OrdinalIgnoreCase))
                      ?? _providers.FirstOrDefault();

        if (primary != null)
        {
            try
            {
                var result = await primary.CompleteAsync(request, cancellationToken);
                if (result.Success)
                {
                    return result;
                }
                _logger.LogWarning("Primary LLM provider {Provider} failed: {Error}. Attempting fallback.", primary.ProviderName, result.ErrorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception calling LLM provider {Provider}. Attempting fallback.", primary.ProviderName);
            }
        }

        // Fallback to any other provider if available
        foreach (var fallback in _providers.Where(p => p != primary))
        {
            try
            {
                _logger.LogInformation("Routing LLM request to fallback provider {Provider}", fallback.ProviderName);
                var fallbackResult = await fallback.CompleteAsync(request, cancellationToken);
                if (fallbackResult.Success)
                {
                    return fallbackResult;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Fallback provider {Provider} also failed.", fallback.ProviderName);
            }
        }

        return new LLMCompletionResult(
            Provider: "None",
            Model: "fail-closed",
            Content: string.Empty,
            PromptTokens: 0,
            CompletionTokens: 0,
            Success: false,
            ErrorMessage: "All LLM providers exhausted or failed."
        );
    }
}

