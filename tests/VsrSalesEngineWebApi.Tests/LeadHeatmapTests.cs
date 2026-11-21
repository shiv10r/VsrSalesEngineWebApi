using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VsrSalesEngineWebApi.Controllers;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Services.AgentRelay;
using VsrSalesEngineWebApi.Infrastructure.Data;
using VsrSalesEngineWebApi.Infrastructure.Repositories;
using Xunit;

namespace VsrSalesEngineWebApi.Tests;

public class LeadHeatmapTests
{
    private static readonly TimeZoneInfo Ist = HeatmapBucketer.ResolveZone();

    private static DateTime Utc(int year, int month, int day, int hour, int minute = 0) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    [Fact]
    public void Build_WithNoData_ReturnsEmptySparseResult()
    {
        var map = HeatmapBucketer.Build(Array.Empty<DateTime>(), Array.Empty<DateTime>(), 28, Ist);

        Assert.Empty(map.Cells);
        Assert.Equal(0, map.TotalNewLeads);
        Assert.Equal(0, map.TotalRuns);
        Assert.Null(map.FirstRunAtUtc);
        Assert.Equal(28, map.Days);
    }

    [Fact]
    public void Build_BucketsByIstDayAndHour_MondayIsZero()
    {
        // Mon 2026-11-23 02:00 UTC = 07:30 IST Monday -> (0, 7). Two leads, one run in that hour.
        var leads = new[] { Utc(2026, 11, 23, 2), Utc(2026, 11, 23, 2, 20) };
        var runs = new[] { Utc(2026, 11, 23, 2, 5) };

        var map = HeatmapBucketer.Build(leads, runs, 28, Ist);

        var cell = Assert.Single(map.Cells);
        Assert.Equal((0, 7, 2, 1), (cell.DayOfWeek, cell.Hour, cell.NewLeads, cell.Runs));
        Assert.Equal(2, map.TotalNewLeads);
        Assert.Equal(1, map.TotalRuns);
    }

    [Fact]
    public void Build_ConvertsToIstAcrossMidnight_SundayUtcBecomesMondayIst()
    {
        // Sun 2026-11-22 20:00 UTC = Mon 01:30 IST.
        var map = HeatmapBucketer.Build(new[] { Utc(2026, 11, 22, 20) }, Array.Empty<DateTime>(), 28, Ist);

        var cell = Assert.Single(map.Cells);
        Assert.Equal(0, cell.DayOfWeek);
        Assert.Equal(1, cell.Hour);
    }

    [Fact]
    public void Build_SundayInIstIsSix_AndCellsAreSortedAndSparse()
    {
        // Sat 2026-11-21 19:00 UTC = Sun 00:30 IST -> (6, 0). Wed 2026-11-25 04:00 UTC = 09:30 IST -> (2, 9).
        var map = HeatmapBucketer.Build(new[] { Utc(2026, 11, 21, 19), Utc(2026, 11, 25, 4) }, Array.Empty<DateTime>(), 28, Ist);

        Assert.Equal(2, map.Cells.Count);
        Assert.Equal((2, 9), (map.Cells[0].DayOfWeek, map.Cells[0].Hour));
        Assert.Equal((6, 0), (map.Cells[1].DayOfWeek, map.Cells[1].Hour));
        Assert.All(map.Cells, c => Assert.True(c.NewLeads + c.Runs > 0));
    }

    [Fact]
    public void Build_FirstRunIsTheEarliestRun()
    {
        var runs = new[] { Utc(2026, 11, 24, 6), Utc(2026, 11, 22, 6), Utc(2026, 11, 23, 6) };

        var map = HeatmapBucketer.Build(Array.Empty<DateTime>(), runs, 28, Ist);

        Assert.Equal(Utc(2026, 11, 22, 6), map.FirstRunAtUtc);
    }

    [Fact]
    public void Build_FallsBackToUtcLabelWhenZoneIsUtc()
    {
        var map = HeatmapBucketer.Build(new[] { Utc(2026, 11, 23, 2) }, Array.Empty<DateTime>(), 28, TimeZoneInfo.Utc);

        Assert.Equal("UTC", map.Timezone);
        Assert.Equal(2, Assert.Single(map.Cells).Hour);
    }

    [Fact]
    public void ResolveZone_UnknownZoneFallsBackToUtc()
    {
        Assert.Equal(TimeZoneInfo.Utc.Id, HeatmapBucketer.ResolveZone("Not/AZone").Id);
    }

    private static (LeadHeatmapService Service, VsrDbContext Context) Create()
    {
        var context = TestDbContextFactory.CreateSeeded();
        var service = new LeadHeatmapService(new OpportunityRepository(context), new AgentRunRepository(context));
        return (service, context);
    }

    [Theory]
    [InlineData(1, 7)]
    [InlineData(28, 28)]
    [InlineData(500, 90)]
    public async Task Service_ClampsDaysToSevenThroughNinety(int requested, int expected)
    {
        var (service, _) = Create();

        var map = await service.GetHeatmapAsync(requested);

        Assert.Equal(expected, map.Days);
    }

    [Fact]
    public async Task Service_CountsRealRunsAndLeadsInsideTheWindowOnly()
    {
        var (service, context) = Create();
        var before = await service.GetHeatmapAsync(28);

        context.AgentRuns.Add(new AgentRun { Id = "RUN-HM-IN", StartedAtUtc = DateTime.UtcNow.AddHours(-2) });
        context.AgentRuns.Add(new AgentRun { Id = "RUN-HM-OLD", StartedAtUtc = DateTime.UtcNow.AddDays(-60) });
        context.Opportunities.Add(new Opportunity { Id = "OP-HM-IN", Company = "Acme", Title = "Dev", DeduplicationKey = "hm-in", RetrievedAtUtc = DateTime.UtcNow.AddHours(-1) });
        context.Opportunities.Add(new Opportunity { Id = "OP-HM-OLD", Company = "Old", Title = "Dev", DeduplicationKey = "hm-old", RetrievedAtUtc = DateTime.UtcNow.AddDays(-60) });
        await context.SaveChangesAsync();

        var after = await service.GetHeatmapAsync(28);

        Assert.Equal(before.TotalRuns + 1, after.TotalRuns);
        Assert.Equal(before.TotalNewLeads + 1, after.TotalNewLeads);
        Assert.Equal(after.TotalRuns, after.Cells.Sum(c => c.Runs));
        Assert.Equal(after.TotalNewLeads, after.Cells.Sum(c => c.NewLeads));
    }

    [Fact]
    public void Controller_ExposesAuthorizedGetHeatmapRoute()
    {
        var method = typeof(AgentActivityController).GetMethod(nameof(AgentActivityController.GetHeatmap))!;

        var route = Assert.Single(method.GetCustomAttributes(typeof(HttpGetAttribute), true).Cast<HttpGetAttribute>());
        Assert.Equal("heatmap", route.Template);
        Assert.True(typeof(AgentActivityController).IsDefined(typeof(AuthorizeAttribute), true));
    }
}
