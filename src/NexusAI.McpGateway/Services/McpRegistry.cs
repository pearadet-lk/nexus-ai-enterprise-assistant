using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using NexusAI.Contracts.Mcp;
using NexusAI.McpGateway.Configuration;

namespace NexusAI.McpGateway.Services;

public interface IMcpRegistry
{
    IReadOnlyList<McpToolDto> GetTools();

    IReadOnlyList<McpServerHealthDto> GetHealth();

    Task<McpRefreshResult> RefreshAsync(CancellationToken cancellationToken);

    Task<McpExecuteToolResult> ExecuteAsync(
        McpExecuteToolRequest request,
        CancellationToken cancellationToken);
}

public sealed class McpRegistry(
    IOptions<List<McpServerOptions>> serverOptions,
    IDistributedCache cache,
    ILogger<McpRegistry> logger) : IMcpRegistry, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private const string ToolsCacheKey = "mcp:tools";

    private readonly SemaphoreSlim _sync = new(1, 1);
    private readonly Dictionary<string, McpServerConnection> _connections = new(StringComparer.OrdinalIgnoreCase);
    private List<McpToolDto> _tools = [];

    public IReadOnlyList<McpToolDto> GetTools()
    {
        if (_tools.Count > 0)
        {
            return _tools;
        }

        var cached = cache.GetString(ToolsCacheKey);
        if (!string.IsNullOrWhiteSpace(cached))
        {
            _tools = JsonSerializer.Deserialize<List<McpToolDto>>(cached, JsonOptions) ?? [];
        }

        return _tools;
    }

    public IReadOnlyList<McpServerHealthDto> GetHealth() =>
        _connections.Values
            .Select(connection => new McpServerHealthDto(
                connection.ServerId,
                connection.ServerName,
                connection.IsHealthy,
                connection.LatencyMs,
                connection.Error,
                connection.ToolCount))
            .ToList();

    public async Task<McpRefreshResult> RefreshAsync(CancellationToken cancellationToken)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            await DisposeConnectionsAsync();
            _tools = [];

            foreach (var server in serverOptions.Value)
            {
                var connection = new McpServerConnection(server.Id, server.Name, server.Endpoint);
                try
                {
                    var sw = Stopwatch.StartNew();
                    var transport = new HttpClientTransport(new HttpClientTransportOptions
                    {
                        Endpoint = new Uri(server.Endpoint),
                        TransportMode = HttpTransportMode.StreamableHttp
                    });

                    connection.Client = await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
                    sw.Stop();
                    connection.LatencyMs = (int)sw.ElapsedMilliseconds;
                    connection.IsHealthy = true;

                    var tools = await connection.Client.ListToolsAsync(cancellationToken: cancellationToken);
                    connection.ToolCount = tools.Count;
                    _tools.AddRange(tools.Select(tool => new McpToolDto(
                        server.Id,
                        server.Name,
                        tool.Name,
                        tool.Description,
                        tool.JsonSchema.GetRawText())));

                    _connections[server.Id] = connection;
                    logger.LogInformation(
                        "Connected to MCP server {ServerId} with {ToolCount} tools",
                        server.Id,
                        tools.Count);
                }
                catch (Exception ex)
                {
                    connection.IsHealthy = false;
                    connection.Error = ex.Message;
                    _connections[server.Id] = connection;
                    logger.LogError(ex, "Failed to connect to MCP server {ServerId}", server.Id);
                }
            }

            await cache.SetStringAsync(
                ToolsCacheKey,
                JsonSerializer.Serialize(_tools, JsonOptions),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) },
                cancellationToken);

            return new McpRefreshResult(_connections.Count, _tools.Count);
        }
        finally
        {
            _sync.Release();
        }
    }

    public async Task<McpExecuteToolResult> ExecuteAsync(
        McpExecuteToolRequest request,
        CancellationToken cancellationToken)
    {
        if (!_connections.TryGetValue(request.ServerId, out var connection) || connection.Client is null)
        {
            throw new InvalidOperationException($"MCP server '{request.ServerId}' is not connected.");
        }

        var sw = Stopwatch.StartNew();
        var result = await connection.Client.CallToolAsync(
            request.ToolName,
            request.Arguments ?? new Dictionary<string, object?>(),
            cancellationToken: cancellationToken);
        sw.Stop();

        var content = string.Join(
            Environment.NewLine,
            result.Content.OfType<TextContentBlock>().Select(block => block.Text));

        return new McpExecuteToolResult(
            request.ServerId,
            request.ToolName,
            content,
            (int)sw.ElapsedMilliseconds);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeConnectionsAsync();
        _sync.Dispose();
    }

    private async Task DisposeConnectionsAsync()
    {
        foreach (var connection in _connections.Values)
        {
            if (connection.Client is not null)
            {
                await connection.Client.DisposeAsync();
            }
        }

        _connections.Clear();
    }

    private sealed class McpServerConnection(string serverId, string serverName, string endpoint)
    {
        public string ServerId { get; } = serverId;

        public string ServerName { get; } = serverName;

        public string Endpoint { get; } = endpoint;

        public McpClient? Client { get; set; }

        public bool IsHealthy { get; set; }

        public int LatencyMs { get; set; }

        public string? Error { get; set; }

        public int ToolCount { get; set; }
    }
}
