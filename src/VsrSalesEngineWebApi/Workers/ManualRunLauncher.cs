using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Workers;

/// <summary>
/// Starts a manual agent run without holding the HTTP request open. The run row is created first
/// (so a second click gets a 409), then the relay runs in its own DI scope in the background.
/// </summary>
public class ManualRunLauncher : IManualRunLauncher
{
    private static readonly SemaphoreSlim StartGate = new(1, 1);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ManualRunLauncher> _logger;

    public ManualRunLauncher(IServiceScopeFactory scopes, ILogger<ManualRunLauncher> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    public async Task<string?> StartAsync(string user)
    {
        RelayRequest request;
        string runId;

        await StartGate.WaitAsync();
        try
        {
            using var scope = _scopes.CreateScope();
            var relay = scope.ServiceProvider.GetRequiredService<IAgentRelay>();
            request = await relay.BuildManualRequestAsync(user);

            var run = await relay.BeginRunAsync(request);
            if (run == null) return null;
            runId = run.Id;
        }
        finally
        {
            StartGate.Release();
        }

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var relay = scope.ServiceProvider.GetRequiredService<IAgentRelay>();
                await relay.ExecuteAsync(runId, request, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Manual agent run {RunId} failed outside the relay.", runId);
            }
        });

        return runId;
    }
}
