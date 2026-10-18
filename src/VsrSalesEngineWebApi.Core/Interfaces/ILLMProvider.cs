namespace VsrSalesEngineWebApi.Core.Interfaces;

public record LLMCompletionRequest(
    string Prompt,
    string? SystemInstruction = null,
    double Temperature = 0.2,
    int MaxTokens = 1024,
    IReadOnlyDictionary<string, string>? Metadata = null
);

public record LLMCompletionResult(
    string Provider,
    string Model,
    string Content,
    int PromptTokens,
    int CompletionTokens,
    bool Success,
    string? ErrorMessage = null
);

public interface ILLMProvider
{
    string ProviderName { get; }
    string DefaultModel { get; }
    Task<LLMCompletionResult> CompleteAsync(LLMCompletionRequest request, CancellationToken cancellationToken = default);
}

public interface ILLMRouter
{
    string DefaultProviderName { get; }
    Task<LLMCompletionResult> RouteCompletionAsync(LLMCompletionRequest request, string? preferredProvider = null, CancellationToken cancellationToken = default);
}

