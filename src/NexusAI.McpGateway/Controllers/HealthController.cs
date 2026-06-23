using Microsoft.AspNetCore.Mvc;

namespace NexusAI.McpGateway.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { service = "NexusAI.McpGateway", status = "healthy" });
}
