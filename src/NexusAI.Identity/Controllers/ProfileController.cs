using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusAI.Contracts.Auth;
using NexusAI.Contracts.Common;
using System.Security.Claims;

namespace NexusAI.Identity.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class ProfileController : ControllerBase
{
    [HttpGet("me")]
    public ActionResult<ApiResponse<UserProfileDto>> GetProfile()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? string.Empty;

        var email = User.FindFirstValue(ClaimTypes.Email)
            ?? User.FindFirstValue("email");

        var displayName = User.FindFirstValue("preferred_username")
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? email;

        var roles = User.FindAll(ClaimTypes.Role)
            .Select(x => x.Value)
            .Concat(User.FindAll("realm_access").SelectMany(_ => Array.Empty<string>()))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        if (roles.Count == 0)
        {
            roles = User.FindAll("groups").Select(x => x.Value).ToList();
        }

        var profile = new UserProfileDto(userId, email, displayName, roles);
        return Ok(ApiResponse<UserProfileDto>.Ok(profile));
    }
}
