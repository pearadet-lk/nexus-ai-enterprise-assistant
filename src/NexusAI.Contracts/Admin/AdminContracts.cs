using NexusAI.Contracts.Chat;

namespace NexusAI.Contracts.Admin;

public sealed record AdminStatsDto(
    int TotalAuditEvents,
    int TotalPromptTokens,
    int TotalCompletionTokens,
    decimal TotalCost,
    int TotalToolExecutions,
    int TotalConversations);

public sealed record AdminDashboardDto(
    AdminStatsDto Stats,
    IReadOnlyList<AuditLogDto> RecentAuditLogs,
    IReadOnlyList<ToolExecutionDto> RecentToolExecutions);
