using Microsoft.AspNetCore.Mvc;

namespace NexusAI.Identity.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { service = "NexusAI.Identity", status = "healthy" });
}
