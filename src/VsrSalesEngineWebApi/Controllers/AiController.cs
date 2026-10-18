using Microsoft.AspNetCore.Mvc;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Controllers;

[ApiController]
[Route("api/v1/ai")]
public class AiController : ControllerBase
{
    private readonly IAiFormService _aiFormService;
    private readonly ILLMRouter _llmRouter;
    private readonly ILogger<AiController> _logger;

    public AiController(
        IAiFormService aiFormService,
        ILLMRouter llmRouter,
        ILogger<AiController> logger)
    {
        _aiFormService = aiFormService;
        _llmRouter = llmRouter;
        _logger = logger;
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            status = "Operational",
            defaultProvider = _llmRouter.DefaultProviderName,
            availableProviders = new[] { "Gemini" },
            defaultModel = "gemini-2.5-flash",
            capabilities = new[]
            {
                "OpportunityParsing",
                "ContactExtraction",
                "CampaignGeneration",
                "OutreachDraftPersonalization"
            }
        });
    }

    [HttpPost("parse-form")]
    [ProducesResponseType(typeof(AiFormParseResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ParseForm([FromBody] AiFormParseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest(new { message = "Prompt cannot be empty." });
        }

        var result = await _aiFormService.ParseFormPromptAsync(request, HttpContext.RequestAborted);
        return Ok(result);
    }

    [HttpPost("complete")]
    [ProducesResponseType(typeof(LLMCompletionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Complete([FromBody] LLMCompletionRequest request, [FromQuery] string? provider = null)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest(new { message = "Prompt cannot be empty." });
        }

        var result = await _llmRouter.RouteCompletionAsync(request, provider, HttpContext.RequestAborted);
        return Ok(result);
    }
}
