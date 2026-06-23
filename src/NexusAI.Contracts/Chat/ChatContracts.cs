namespace NexusAI.Contracts.Chat;

public sealed record ChatRequest(Guid? ConversationId, string Message);

public sealed record ToolExecutionDto(
    Guid Id,
    Guid? ConversationId,
    string ToolName,
    int DurationMs,
    string Status,
    string? ErrorMessage,
    DateTime CreatedAt);

public sealed record LogToolExecutionRequest(
    Guid? ConversationId,
    string ToolName,
    int DurationMs,
    string Status,
    string? ErrorMessage = null);

public sealed record AuditLogDto(
    Guid Id,
    string? UserId,
    string? Action,
    int PromptTokens,
    int CompletionTokens,
    decimal Cost,
    DateTime CreatedAt);

public sealed record LogAuditRequest(
    string? Action,
    int PromptTokens,
    int CompletionTokens,
    decimal Cost);
