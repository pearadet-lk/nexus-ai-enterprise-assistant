using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexusAI.ContextService.Data;
using NexusAI.ContextService.Entities;
using NexusAI.Contracts.Common;
using NexusAI.Contracts.Conversations;
using NexusAI.SharedKernel.Constants;
using System.Security.Claims;

namespace NexusAI.ContextService.Controllers;

[ApiController]
[Route("api/conversations")]
[Authorize]
public sealed class ConversationsController(NexusDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ConversationDto>>>> List(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var conversations = await db.Conversations
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Select(x => new ConversationDto(
                x.Id,
                x.UserId,
                x.Title,
                x.CreatedAt,
                x.UpdatedAt,
                Array.Empty<MessageDto>()))
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<ConversationDto>>.Ok(conversations));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ConversationDto>>> Get(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var conversation = await db.Conversations
            .AsNoTracking()
            .Include(x => x.Messages.OrderBy(m => m.CreatedAt))
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);

        if (conversation is null)
        {
            return NotFound(ApiResponse<ConversationDto>.Fail("Conversation not found."));
        }

        return Ok(ApiResponse<ConversationDto>.Ok(ToDto(conversation)));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ConversationDto>>> Create(
        [FromBody] CreateConversationRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var conversation = new Conversation
        {
            UserId = userId,
            Title = string.IsNullOrWhiteSpace(request.Title) ? "New conversation" : request.Title.Trim(),
            UpdatedAt = DateTime.UtcNow
        };

        db.Conversations.Add(conversation);
        await db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(
            nameof(Get),
            new { id = conversation.Id },
            ApiResponse<ConversationDto>.Ok(ToDto(conversation)));
    }

    [HttpPost("{id:guid}/messages")]
    public async Task<ActionResult<ApiResponse<MessageDto>>> AddMessage(
        Guid id,
        [FromBody] AddMessageRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(ApiResponse<MessageDto>.Fail("Message content is required."));
        }

        var userId = GetUserId();
        var conversation = await db.Conversations
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);

        if (conversation is null)
        {
            return NotFound(ApiResponse<MessageDto>.Fail("Conversation not found."));
        }

        var role = string.IsNullOrWhiteSpace(request.Role) ? MessageRoles.User : request.Role.Trim().ToLowerInvariant();
        var message = new Message
        {
            ConversationId = conversation.Id,
            Role = role,
            Content = request.Content.Trim()
        };

        conversation.UpdatedAt = DateTime.UtcNow;
        db.Messages.Add(message);
        await db.SaveChangesAsync(cancellationToken);

        var dto = new MessageDto(message.Id, message.Role, message.Content, message.CreatedAt);
        return Ok(ApiResponse<MessageDto>.Ok(dto));
    }

    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? throw new InvalidOperationException("User identifier claim is missing.");

    private static ConversationDto ToDto(Conversation conversation) =>
        new(
            conversation.Id,
            conversation.UserId,
            conversation.Title,
            conversation.CreatedAt,
            conversation.UpdatedAt,
            conversation.Messages
                .OrderBy(x => x.CreatedAt)
                .Select(x => new MessageDto(x.Id, x.Role, x.Content, x.CreatedAt))
                .ToList());
}
