using Microsoft.EntityFrameworkCore;
using VsrSalesEngineWebApi.Infrastructure.Data;

namespace VsrSalesEngineWebApi.Tests;

public static class TestDbContextFactory
{
    /// <summary>The production seed only: configuration, no invented business data, everything switched off.</summary>
    public static VsrDbContext CreateDefaultSeeded()
    {
        var options = new DbContextOptionsBuilder<VsrDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        var context = new VsrDbContext(options);
        context.Database.EnsureCreated();
        VsrDbSeeder.SeedAsync(context).GetAwaiter().GetResult();
        return context;
    }

    /// <summary>The production seed plus test-only fixtures: sample leads, campaigns and live feeds switched on.</summary>
    public static VsrDbContext CreateSeeded()
    {
        var context = CreateDefaultSeeded();
        TestFixtureSwitches.ApplyAsync(context).GetAwaiter().GetResult();
        TestFixtureData.AddAsync(context).GetAwaiter().GetResult();
        return context;
    }
}
