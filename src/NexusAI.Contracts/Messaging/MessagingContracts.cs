namespace NexusAI.Contracts.Messaging;

public sealed record AuditLoggedMessage(
    Guid AuditId,
    string? UserId,
    string? Action,
    int PromptTokens,
    int CompletionTokens,
    decimal Cost,
    DateTime CreatedAt);

public sealed record NotificationMessage(
    string Channel,
    string Subject,
    string Body,
    string? UserId);

public static class NexusQueues
{
    public const string Audit = "nexusai.audit";

    public const string Notifications = "nexusai.notifications";
}
