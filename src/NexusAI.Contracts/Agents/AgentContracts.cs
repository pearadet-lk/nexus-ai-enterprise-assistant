namespace NexusAI.Contracts.Agents;

public sealed record AgentPlanStep(
    int Order,
    string Title,
    string? Description,
    string? ToolServerId,
    string? ToolName,
    Dictionary<string, object?>? Arguments);

public sealed record AgentPlan(IReadOnlyList<AgentPlanStep> Steps);

public sealed record ConversationMemoryDto(
    Guid ConversationId,
    string? Summary,
    string? PreferencesJson,
    DateTime? UpdatedAt);

public sealed record UpsertConversationMemoryRequest(
    string? Summary,
    string? PreferencesJson);

public sealed record AgentPhaseEvent(
    string Phase,
    string Status,
    string? Message = null);

public sealed record AgentStepEvent(
    int Order,
    string Title,
    string Status,
    string? Result = null);

public sealed record AgentReviewEvent(
    bool Approved,
    string Feedback,
    bool Retried);
