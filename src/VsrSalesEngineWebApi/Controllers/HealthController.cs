using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VsrSalesEngineWebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    private readonly IHostEnvironment _env;

    public HealthController(IHostEnvironment env)
    {
        _env = env;
    }

    [HttpGet]
    public IActionResult Check()
    {
        return Ok(new
        {
            status = "Healthy",
            service = "VsrSalesEngineWebApi",
            version = "0.1.0",
            environment = _env.EnvironmentName,
            framework = ".NET 10 LTS",
            timestampUtc = DateTime.UtcNow
        });
    }
}

