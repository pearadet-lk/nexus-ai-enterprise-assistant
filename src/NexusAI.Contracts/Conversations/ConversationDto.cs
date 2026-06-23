namespace NexusAI.Contracts.Conversations;

public sealed record ConversationDto(
    Guid Id,
    string UserId,
    string? Title,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<MessageDto> Messages);

public sealed record MessageDto(
    Guid Id,
    string Role,
    string Content,
    DateTime CreatedAt);

public sealed record CreateConversationRequest(string? Title);

public sealed record AddMessageRequest(string Role, string Content);
