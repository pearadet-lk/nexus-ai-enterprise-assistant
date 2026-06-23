using NexusAI.Contracts.Agents;
using NexusAI.Contracts.Chat;
using NexusAI.Contracts.Conversations;

namespace NexusAI.AgentService.Services;

public interface IContextApiClient
{
    Task<ConversationDto> CreateConversationAsync(string? title, CancellationToken cancellationToken);

    Task<ConversationDto> GetConversationAsync(Guid id, CancellationToken cancellationToken);

    Task<MessageDto> AddMessageAsync(Guid conversationId, string role, string content, CancellationToken cancellationToken);

    Task<ToolExecutionDto> LogToolExecutionAsync(
        Guid conversationId,
        string toolName,
        int durationMs,
        string status,
        string? errorMessage,
        CancellationToken cancellationToken);

    Task LogAuditAsync(
        string action,
        int promptTokens,
        int completionTokens,
        decimal cost,
        CancellationToken cancellationToken);

    Task<ConversationMemoryDto> GetMemoryAsync(Guid conversationId, CancellationToken cancellationToken);

    Task<ConversationMemoryDto> UpsertMemoryAsync(
        Guid conversationId,
        string? summary,
        string? preferencesJson,
        CancellationToken cancellationToken);
}
