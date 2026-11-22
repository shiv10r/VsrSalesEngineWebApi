using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Moq;
using VsrSalesEngineWebApi.Controllers;
using Xunit;

namespace VsrSalesEngineWebApi.Tests;

public class DeploymentAndOperationalSmokeTests
{
    [Fact]
    public void RootHealthEndpoint_ReturnsHealthyStatus()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns("Production");

        var actionResult = new HealthController(env.Object).Check();
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.NotNull(okResult.Value);

        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal("Healthy", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("VsrSalesEngineWebApi", doc.RootElement.GetProperty("service").GetString());
        Assert.Equal("Production", doc.RootElement.GetProperty("environment").GetString());
    }
}
