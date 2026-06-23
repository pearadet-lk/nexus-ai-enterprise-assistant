namespace NexusAI.Contracts.Mcp;

public sealed record McpToolDto(
    string ServerId,
    string ServerName,
    string Name,
    string? Description,
    string? InputSchemaJson);

public sealed record McpServerHealthDto(
    string ServerId,
    string ServerName,
    bool IsHealthy,
    int LatencyMs,
    string? Error,
    int ToolCount);

public sealed record McpExecuteToolRequest(
    string ServerId,
    string ToolName,
    Dictionary<string, object?>? Arguments);

public sealed record McpExecuteToolResult(
    string ServerId,
    string ToolName,
    string Content,
    int DurationMs);

public sealed record McpRefreshResult(int ServerCount, int ToolCount);
