using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexusAI.ContextService.Data;
using NexusAI.Contracts.Admin;
using NexusAI.Contracts.Chat;
using NexusAI.Contracts.Common;

namespace NexusAI.ContextService.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "admin")]
public sealed class AdminController(NexusDbContext db) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<ApiResponse<AdminDashboardDto>>> GetDashboard(CancellationToken cancellationToken)
    {
        var stats = await BuildStatsAsync(cancellationToken);
        var recentAudit = await db.AuditLogs
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(20)
            .Select(x => new AuditLogDto(
                x.Id,
                x.UserId,
                x.Action,
                x.PromptTokens,
                x.CompletionTokens,
                x.Cost,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        var recentTools = await db.ToolExecutions
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(20)
            .Select(x => new ToolExecutionDto(
                x.Id,
                x.ConversationId,
                x.ToolName,
                x.DurationMs,
                x.Status,
                x.ErrorMessage,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<AdminDashboardDto>.Ok(new AdminDashboardDto(stats, recentAudit, recentTools)));
    }

    [HttpGet("stats")]
    public async Task<ActionResult<ApiResponse<AdminStatsDto>>> GetStats(CancellationToken cancellationToken) =>
        Ok(ApiResponse<AdminStatsDto>.Ok(await BuildStatsAsync(cancellationToken)));

    [HttpGet("audit-logs")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AuditLogDto>>>> GetAuditLogs(CancellationToken cancellationToken)
    {
        var logs = await db.AuditLogs
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .Select(x => new AuditLogDto(
                x.Id,
                x.UserId,
                x.Action,
                x.PromptTokens,
                x.CompletionTokens,
                x.Cost,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<AuditLogDto>>.Ok(logs));
    }

    [HttpGet("tool-executions")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ToolExecutionDto>>>> GetToolExecutions(CancellationToken cancellationToken)
    {
        var executions = await db.ToolExecutions
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .Select(x => new ToolExecutionDto(
                x.Id,
                x.ConversationId,
                x.ToolName,
                x.DurationMs,
                x.Status,
                x.ErrorMessage,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<ToolExecutionDto>>.Ok(executions));
    }

    private async Task<AdminStatsDto> BuildStatsAsync(CancellationToken cancellationToken)
    {
        var audit = await db.AuditLogs.AsNoTracking().ToListAsync(cancellationToken);
        var toolCount = await db.ToolExecutions.AsNoTracking().CountAsync(cancellationToken);
        var conversationCount = await db.Conversations.AsNoTracking().CountAsync(cancellationToken);

        return new AdminStatsDto(
            audit.Count,
            audit.Sum(x => x.PromptTokens),
            audit.Sum(x => x.CompletionTokens),
            audit.Sum(x => x.Cost),
            toolCount,
            conversationCount);
    }
}
