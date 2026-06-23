using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusAI.Contracts.Common;
using NexusAI.Contracts.Mcp;
using NexusAI.McpGateway.Services;

namespace NexusAI.McpGateway.Controllers;

[ApiController]
[Route("api/mcp")]
[Authorize]
public sealed class McpController(IMcpRegistry registry) : ControllerBase
{
    [HttpGet("tools")]
    public ActionResult<ApiResponse<IReadOnlyList<McpToolDto>>> GetTools() =>
        Ok(ApiResponse<IReadOnlyList<McpToolDto>>.Ok(registry.GetTools()));

    [HttpGet("health")]
    public ActionResult<ApiResponse<IReadOnlyList<McpServerHealthDto>>> GetHealth() =>
        Ok(ApiResponse<IReadOnlyList<McpServerHealthDto>>.Ok(registry.GetHealth()));

    [HttpPost("refresh")]
    public async Task<ActionResult<ApiResponse<McpRefreshResult>>> Refresh(CancellationToken cancellationToken)
    {
        var result = await registry.RefreshAsync(cancellationToken);
        return Ok(ApiResponse<McpRefreshResult>.Ok(result));
    }

    [HttpPost("execute")]
    public async Task<ActionResult<ApiResponse<McpExecuteToolResult>>> Execute(
        [FromBody] McpExecuteToolRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ServerId) || string.IsNullOrWhiteSpace(request.ToolName))
        {
            return BadRequest(ApiResponse<McpExecuteToolResult>.Fail("ServerId and ToolName are required."));
        }

        var result = await registry.ExecuteAsync(request, cancellationToken);
        return Ok(ApiResponse<McpExecuteToolResult>.Ok(result));
    }
}
