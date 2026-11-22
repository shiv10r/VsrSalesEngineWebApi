namespace VsrSalesEngineWebApi.Core.DTOs;

public record AgentLeadDto(
    string? OpportunityId,
    string Company,
    string Title,
    string Source,
    int? Score,
    IReadOnlyList<string> Reasons,
    string? Url);

public record AgentEventDto(
    long Id,
    string RunId,
    DateTime AtUtc,
    string AgentId,
    string Kind,
    string Message,
    AgentLeadDto? Lead);

public record AgentRunDto(
    string Id,
    string ScheduleId,
    string ShiftName,
    string Trigger,
    DateTime StartedAtUtc,
    DateTime? FinishedAtUtc,
    string Status,
    string? Summary,
    int NewLeads,
    int Duplicates,
    int Rejected);

public record AgentLifetimeDto(int ItemsProcessed, int EventsLogged);

public record AgentCardDto(
    string Id,
    string Name,
    string Role,
    string Status, // working, idle, waiting, error
    string? CurrentTask,
    DateTime? LastActiveAtUtc,
    AgentLifetimeDto Lifetime);

public record AgentOverviewDto(
    IReadOnlyList<AgentCardDto> Agents,
    AgentRunDto? LastRun,
    DateTime? NextRunAtUtc,
    bool RunInProgress);

public record RunNowResultDto(string RunId);
