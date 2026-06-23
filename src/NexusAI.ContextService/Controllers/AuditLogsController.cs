using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Mvc;

using NexusAI.ContextService.Data;

using NexusAI.ContextService.Entities;

using NexusAI.Contracts.Chat;

using NexusAI.Contracts.Common;

using NexusAI.Contracts.Messaging;

using NexusAI.SharedKernel.Messaging;

using System.Security.Claims;



namespace NexusAI.ContextService.Controllers;



[ApiController]

[Route("api/audit-logs")]

[Authorize]

public sealed class AuditLogsController(

    NexusDbContext db,

    IRabbitMqPublisher rabbitMqPublisher) : ControllerBase

{

    [HttpPost]

    public async Task<ActionResult<ApiResponse<AuditLogDto>>> Log(

        [FromBody] LogAuditRequest request,

        CancellationToken cancellationToken)

    {

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)

            ?? User.FindFirstValue("sub");



        var audit = new AuditLog

        {

            UserId = userId,

            Action = request.Action,

            PromptTokens = request.PromptTokens,

            CompletionTokens = request.CompletionTokens,

            Cost = request.Cost

        };



        db.AuditLogs.Add(audit);

        await db.SaveChangesAsync(cancellationToken);



        await rabbitMqPublisher.PublishAsync(

            NexusQueues.Audit,

            new AuditLoggedMessage(

                audit.Id,

                audit.UserId,

                audit.Action,

                audit.PromptTokens,

                audit.CompletionTokens,

                audit.Cost,

                audit.CreatedAt),

            cancellationToken);



        var dto = new AuditLogDto(

            audit.Id,

            audit.UserId,

            audit.Action,

            audit.PromptTokens,

            audit.CompletionTokens,

            audit.Cost,

            audit.CreatedAt);



        return Ok(ApiResponse<AuditLogDto>.Ok(dto));

    }

}


