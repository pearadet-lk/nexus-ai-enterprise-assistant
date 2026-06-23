using NexusAI.SharedKernel.Entities;

namespace NexusAI.ContextService.Entities;

public sealed class Conversation : EntityBase
{
    public required string UserId { get; set; }

    public string? Title { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public ICollection<Message> Messages { get; set; } = [];
}

public sealed class Message : EntityBase
{
    public Guid ConversationId { get; set; }

    public Conversation Conversation { get; set; } = null!;

    public required string Role { get; set; }

    public required string Content { get; set; }
}

public sealed class ToolExecution : EntityBase
{
    public Guid? ConversationId { get; set; }

    public required string ToolName { get; set; }

    public int DurationMs { get; set; }

    public required string Status { get; set; }

    public string? ErrorMessage { get; set; }
}

public sealed class AuditLog : EntityBase
{
    public string? UserId { get; set; }

    public string? Action { get; set; }

    public int PromptTokens { get; set; }

    public int CompletionTokens { get; set; }

    public decimal Cost { get; set; }
}
