using Microsoft.AspNetCore.Mvc;

namespace NexusAI.ApiGateway.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { service = "NexusAI.ApiGateway", status = "healthy" });
}
