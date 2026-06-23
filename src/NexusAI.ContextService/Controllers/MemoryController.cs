using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using NexusAI.ContextService.Data;
using NexusAI.ContextService.Entities;
using NexusAI.Contracts.Agents;
using NexusAI.Contracts.Common;
using System.Security.Claims;
using System.Text.Json;

namespace NexusAI.ContextService.Controllers;

[ApiController]
[Route("api/conversations/{conversationId:guid}/memory")]
[Authorize]
public sealed class MemoryController(NexusDbContext db, IDistributedCache cache) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [HttpGet]
    public async Task<ActionResult<ApiResponse<ConversationMemoryDto>>> Get(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (!await OwnsConversationAsync(conversationId, userId, cancellationToken))
        {
            return NotFound(ApiResponse<ConversationMemoryDto>.Fail("Conversation not found."));
        }

        var cacheKey = $"memory:{conversationId}";
        var cached = await cache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(cached))
        {
            var cachedDto = JsonSerializer.Deserialize<ConversationMemoryDto>(cached, JsonOptions);
            if (cachedDto is not null)
            {
                return Ok(ApiResponse<ConversationMemoryDto>.Ok(cachedDto));
            }
        }

        var memory = await db.ConversationMemories
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ConversationId == conversationId, cancellationToken);

        var dto = memory is null
            ? new ConversationMemoryDto(conversationId, null, null, null)
            : new ConversationMemoryDto(conversationId, memory.Summary, memory.PreferencesJson, memory.UpdatedAt);

        await cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(dto, JsonOptions),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15) },
            cancellationToken);

        return Ok(ApiResponse<ConversationMemoryDto>.Ok(dto));
    }

    [HttpPut]
    public async Task<ActionResult<ApiResponse<ConversationMemoryDto>>> Upsert(
        Guid conversationId,
        [FromBody] UpsertConversationMemoryRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var conversation = await db.Conversations
            .FirstOrDefaultAsync(x => x.Id == conversationId && x.UserId == userId, cancellationToken);

        if (conversation is null)
        {
            return NotFound(ApiResponse<ConversationMemoryDto>.Fail("Conversation not found."));
        }

        var memory = await db.ConversationMemories
            .FirstOrDefaultAsync(x => x.ConversationId == conversationId, cancellationToken);

        if (memory is null)
        {
            memory = new ConversationMemory { ConversationId = conversationId };
            db.ConversationMemories.Add(memory);
        }

        memory.Summary = request.Summary;
        memory.PreferencesJson = request.PreferencesJson;
        memory.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        var dto = new ConversationMemoryDto(
            conversationId,
            memory.Summary,
            memory.PreferencesJson,
            memory.UpdatedAt);

        await cache.SetStringAsync(
            $"memory:{conversationId}",
            JsonSerializer.Serialize(dto, JsonOptions),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15) },
            cancellationToken);

        return Ok(ApiResponse<ConversationMemoryDto>.Ok(dto));
    }

    private async Task<bool> OwnsConversationAsync(Guid conversationId, string userId, CancellationToken cancellationToken) =>
        await db.Conversations.AnyAsync(x => x.Id == conversationId && x.UserId == userId, cancellationToken);

    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? throw new InvalidOperationException("User identifier claim is missing.");
}
