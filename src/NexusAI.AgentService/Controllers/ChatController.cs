using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusAI.AgentService.Services;
using NexusAI.Contracts.Chat;

namespace NexusAI.AgentService.Controllers;

[ApiController]
[Route("api/chat")]
[Authorize]
public sealed class ChatController(IChatOrchestrator orchestrator) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [HttpPost]
    public async Task Stream([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsJsonAsync(new { error = "Message is required." }, cancellationToken);
            return;
        }

        var authHeader = Request.Headers.Authorization.ToString();
        var accessToken = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authHeader["Bearer ".Length..]
            : string.Empty;

        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";
        Response.ContentType = "text/event-stream";

        await foreach (var streamEvent in orchestrator.StreamChatAsync(
            request.ConversationId,
            request.Message.Trim(),
            accessToken,
            cancellationToken))
        {
            var json = JsonSerializer.Serialize(streamEvent.Data, JsonOptions);
            await Response.WriteAsync($"event: {streamEvent.Type}\n", cancellationToken);
            await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }
}
