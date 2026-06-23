using System.Net.Http.Headers;
using System.Net.Http.Json;
using NexusAI.Contracts.Common;
using NexusAI.Contracts.Mcp;

namespace NexusAI.AgentService.Services;

public interface IMcpGatewayClient
{
    Task<IReadOnlyList<McpToolDto>> GetToolsAsync(CancellationToken cancellationToken);

    Task<McpExecuteToolResult> ExecuteAsync(
        string serverId,
        string toolName,
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken);
}

public sealed class McpGatewayClient(
    HttpClient httpClient,
    ChatRequestContext requestContext) : IMcpGatewayClient
{
    private void ApplyAuth()
    {
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", requestContext.AccessToken);
    }

    public async Task<IReadOnlyList<McpToolDto>> GetToolsAsync(CancellationToken cancellationToken)
    {
        ApplyAuth();
        var response = await httpClient.GetAsync("/api/mcp/tools", cancellationToken);
        return await ReadDataAsync<IReadOnlyList<McpToolDto>>(response, cancellationToken);
    }

    public async Task<McpExecuteToolResult> ExecuteAsync(
        string serverId,
        string toolName,
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken)
    {
        ApplyAuth();
        var response = await httpClient.PostAsJsonAsync(
            "/api/mcp/execute",
            new McpExecuteToolRequest(serverId, toolName, arguments),
            cancellationToken);

        return await ReadDataAsync<McpExecuteToolResult>(response, cancellationToken);
    }

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<T>>(cancellationToken);
        if (!response.IsSuccessStatusCode || payload is null || !payload.Success || payload.Data is null)
        {
            throw new InvalidOperationException(payload?.Error ?? $"MCP Gateway call failed ({response.StatusCode}).");
        }

        return payload.Data;
    }
}
