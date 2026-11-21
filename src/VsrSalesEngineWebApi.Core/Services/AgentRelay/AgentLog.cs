using System.Text.Json;
using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Core.Services.AgentRelay;

/// <summary>Writes the activity log one event at a time, so a live view sees progress while a run is in flight.</summary>
public class AgentLog
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IAgentRunRepository _repository;
    private readonly string _runId;
    private readonly int _pauseMs;

    public AgentLog(IAgentRunRepository repository, string runId, int pauseMs)
    {
        _repository = repository;
        _runId = runId;
        _pauseMs = pauseMs;
    }

    public async Task EmitAsync(string agentId, string kind, string message, AgentLeadDto? lead = null)
    {
        await _repository.AddEventAsync(new AgentEvent
        {
            RunId = _runId,
            AtUtc = DateTime.UtcNow,
            AgentId = agentId,
            Kind = kind,
            Message = message,
            LeadJson = lead == null ? null : JsonSerializer.Serialize(lead, Json)
        });

        if (_pauseMs > 0) await Task.Delay(_pauseMs);
    }

    public Task ThoughtAsync(string agentId, string message) => EmitAsync(agentId, EventKinds.Thought, message);
    public Task ActionAsync(string agentId, string message, AgentLeadDto? lead = null) => EmitAsync(agentId, EventKinds.Action, message, lead);
    public Task ResultAsync(string agentId, string message, AgentLeadDto? lead = null) => EmitAsync(agentId, EventKinds.Result, message, lead);
    public Task HandoffAsync(string agentId, string message) => EmitAsync(agentId, EventKinds.Handoff, message);
    public Task WarningAsync(string agentId, string message, AgentLeadDto? lead = null) => EmitAsync(agentId, EventKinds.Warning, message, lead);
}
