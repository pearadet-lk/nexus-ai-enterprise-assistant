namespace NexusAI.AgentService.Services;

public sealed class ChatRequestContext
{
    public Guid ConversationId { get; set; }

    public string AccessToken { get; set; } = string.Empty;

    public List<ToolStreamEvent> ToolEvents { get; } = [];
}

public sealed record ToolStreamEvent(string ToolName, int DurationMs, string Status, string? ErrorMessage);
