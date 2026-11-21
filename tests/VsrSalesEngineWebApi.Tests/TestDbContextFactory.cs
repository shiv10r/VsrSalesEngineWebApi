using Microsoft.EntityFrameworkCore;
using VsrSalesEngineWebApi.Infrastructure.Data;

namespace VsrSalesEngineWebApi.Tests;

public static class TestDbContextFactory
{
    public static VsrDbContext CreateSeeded()
    {
        var options = new DbContextOptionsBuilder<VsrDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        var context = new VsrDbContext(options);
        context.Database.EnsureCreated();
        VsrDbSeeder.SeedAsync(context).GetAwaiter().GetResult();
        return context;
    }
}
