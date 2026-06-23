using Microsoft.AspNetCore.Mvc;

namespace NexusAI.ContextService.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { service = "NexusAI.ContextService", status = "healthy" });
}
