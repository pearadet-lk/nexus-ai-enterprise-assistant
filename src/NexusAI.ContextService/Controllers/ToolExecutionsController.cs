using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusAI.ContextService.Data;
using NexusAI.ContextService.Entities;
using NexusAI.Contracts.Chat;
using NexusAI.Contracts.Common;

namespace NexusAI.ContextService.Controllers;

[ApiController]
[Route("api/tool-executions")]
[Authorize]
public sealed class ToolExecutionsController(NexusDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ApiResponse<ToolExecutionDto>>> Log(
        [FromBody] LogToolExecutionRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ToolName))
        {
            return BadRequest(ApiResponse<ToolExecutionDto>.Fail("Tool name is required."));
        }

        var execution = new ToolExecution
        {
            ConversationId = request.ConversationId,
            ToolName = request.ToolName.Trim(),
            DurationMs = request.DurationMs,
            Status = string.IsNullOrWhiteSpace(request.Status) ? "unknown" : request.Status.Trim().ToLowerInvariant(),
            ErrorMessage = request.ErrorMessage
        };

        db.ToolExecutions.Add(execution);
        await db.SaveChangesAsync(cancellationToken);

        var dto = new ToolExecutionDto(
            execution.Id,
            execution.ConversationId,
            execution.ToolName,
            execution.DurationMs,
            execution.Status,
            execution.ErrorMessage,
            execution.CreatedAt);

        return Ok(ApiResponse<ToolExecutionDto>.Ok(dto));
    }
}
