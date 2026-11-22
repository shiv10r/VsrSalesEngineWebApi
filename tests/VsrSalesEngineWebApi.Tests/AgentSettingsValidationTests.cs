using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VsrSalesEngineWebApi.Controllers;
using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Core.Services;
using VsrSalesEngineWebApi.Core.Services.AgentRelay;
using VsrSalesEngineWebApi.Infrastructure.Data;
using VsrSalesEngineWebApi.Infrastructure.Repositories;
using VsrSalesEngineWebApi.Security;
using Xunit;

namespace VsrSalesEngineWebApi.Tests;
public class AgentSettingsValidationTests : AgentSettingsTestBase
{

    public static IEnumerable<object[]> InvalidChanges()
    {
        object[] Case(string field, Func<UpdateAgentSettingsDto, UpdateAgentSettingsDto> change) => new object[] { field, change };

        yield return Case("agents[0].name", u => WithAgent(u, "scout", name: "  "));
        yield return Case("agents[0].name", u => WithAgent(u, "scout", name: new string('a', 25)));
        yield return Case("agents[0].name", u => WithAgent(u, "scout", name: "Ar!un"));
        yield return Case("agents[1].name", u => WithAgent(u, "filter", name: "aarav"));
        yield return Case("agents[0].enabled", u => WithAgent(u, "scout", enabled: false));
        yield return Case("agents[2].enabled", u => WithAgent(u, "dedupe", enabled: false));
        yield return Case("agents[5].enabled", u => WithAgent(u, "reporter", enabled: false));
        yield return Case("agents[0].id", u => u with { Agents = u.Agents!.Select((a, i) => i == 0 ? a with { Id = "nobody" } : a).ToList() });
        yield return Case("agents", u => u with { Agents = u.Agents!.Take(5).ToList() });
        yield return Case("keywords.target", u => u with { Keywords = new UpdateKeywordsDto(new List<string> { " " }, new List<string>()) });
        yield return Case("keywords.excluded[0]", u => u with { Keywords = new UpdateKeywordsDto(new List<string> { "net" }, new List<string> { "a,b" }) });
        yield return Case("shifts[0].intervalHours", u => WithShift(u, s => s with { IntervalHours = 0 }));
        yield return Case("shifts[0].intervalHours", u => WithShift(u, s => s with { IntervalHours = 25 }));
        yield return Case("shifts[0].sources[0]", u => WithShift(u, s => s with { Sources = new List<string> { "Facebook" } }));
        yield return Case("shifts[0].sources", u => WithShift(u, s => s with { Sources = new List<string>() }));
        yield return Case("shifts[0].activeFrom", u => WithShift(u, s => s with { ActiveFrom = "25:00" }));
        yield return Case("shifts[0].activeTo", u => WithShift(u, s => s with { ActiveTo = "6am" }));
        yield return Case("shifts[0].scheduleId", u => WithShift(u, s => s with { ScheduleId = "SCHED-NOPE" }));
    }

    [Theory]
    [MemberData(nameof(InvalidChanges))]
    public async Task Save_RejectsInvalidInput_WithTheExactField_AndChangesNothing(
        string field, Func<UpdateAgentSettingsDto, UpdateAgentSettingsDto> change)
    {
        var before = await _settings.GetAsync();

        var result = await SaveAsync(change(await CurrentAsync()));

        Assert.False(result.Ok);
        Assert.Contains(result.Errors, e => e.Field == field && e.Message.Length > 0);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(before), System.Text.Json.JsonSerializer.Serialize(await _settings.GetAsync()));
        Assert.Empty(await _runs.GetEventsAsync(0, 50, AgentSettingsService.SettingsEventRunId));
    }

    [Fact]
    public async Task Compliance_MayBeOff_OnlyWhenTheDrafterIsOffToo()
    {
        var u = WithAgent(await CurrentAsync(), "drafter", enabled: false);
        u = WithAgent(u, "compliance", enabled: false);

        Assert.True((await SaveAsync(u)).Ok);
    }

}
