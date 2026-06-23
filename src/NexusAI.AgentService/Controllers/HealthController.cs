using Microsoft.AspNetCore.Mvc;

namespace NexusAI.AgentService.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { service = "NexusAI.AgentService", status = "healthy" });
}
