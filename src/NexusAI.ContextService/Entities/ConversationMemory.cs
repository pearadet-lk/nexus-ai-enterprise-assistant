using NexusAI.SharedKernel.Entities;

namespace NexusAI.ContextService.Entities;

public sealed class ConversationMemory
{
    public Guid ConversationId { get; set; }

    public Conversation Conversation { get; set; } = null!;

    public string? Summary { get; set; }

    public string? PreferencesJson { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
