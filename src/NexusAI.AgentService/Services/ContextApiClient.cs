using System.Net.Http.Headers;
using System.Net.Http.Json;
using NexusAI.Contracts.Agents;
using NexusAI.Contracts.Chat;
using NexusAI.Contracts.Common;
using NexusAI.Contracts.Conversations;

namespace NexusAI.AgentService.Services;

public sealed class ContextApiClient(
    HttpClient httpClient,
    ChatRequestContext requestContext) : IContextApiClient
{
    private void ApplyAuth()
    {
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", requestContext.AccessToken);
    }

    public async Task<ConversationDto> CreateConversationAsync(string? title, CancellationToken cancellationToken)
    {
        ApplyAuth();
        var response = await httpClient.PostAsJsonAsync(
            "/api/conversations",
            new CreateConversationRequest(title),
            cancellationToken);

        return await ReadDataAsync<ConversationDto>(response, cancellationToken);
    }

    public async Task<ConversationDto> GetConversationAsync(Guid id, CancellationToken cancellationToken)
    {
        ApplyAuth();
        var response = await httpClient.GetAsync($"/api/conversations/{id}", cancellationToken);
        return await ReadDataAsync<ConversationDto>(response, cancellationToken);
    }

    public async Task<MessageDto> AddMessageAsync(
        Guid conversationId,
        string role,
        string content,
        CancellationToken cancellationToken)
    {
        ApplyAuth();
        var response = await httpClient.PostAsJsonAsync(
            $"/api/conversations/{conversationId}/messages",
            new AddMessageRequest(role, content),
            cancellationToken);

        return await ReadDataAsync<MessageDto>(response, cancellationToken);
    }

    public async Task<ToolExecutionDto> LogToolExecutionAsync(
        Guid conversationId,
        string toolName,
        int durationMs,
        string status,
        string? errorMessage,
        CancellationToken cancellationToken)
    {
        ApplyAuth();
        var response = await httpClient.PostAsJsonAsync(
            "/api/tool-executions",
            new LogToolExecutionRequest(conversationId, toolName, durationMs, status, errorMessage),
            cancellationToken);

        return await ReadDataAsync<ToolExecutionDto>(response, cancellationToken);
    }

    public async Task LogAuditAsync(
        string action,
        int promptTokens,
        int completionTokens,
        decimal cost,
        CancellationToken cancellationToken)
    {
        ApplyAuth();
        var response = await httpClient.PostAsJsonAsync(
            "/api/audit-logs",
            new LogAuditRequest(action, promptTokens, completionTokens, cost),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Failed to log audit entry: {body}");
        }
    }

    public async Task<ConversationMemoryDto> GetMemoryAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        ApplyAuth();
        var response = await httpClient.GetAsync($"/api/conversations/{conversationId}/memory", cancellationToken);
        return await ReadDataAsync<ConversationMemoryDto>(response, cancellationToken);
    }

    public async Task<ConversationMemoryDto> UpsertMemoryAsync(
        Guid conversationId,
        string? summary,
        string? preferencesJson,
        CancellationToken cancellationToken)
    {
        ApplyAuth();
        var response = await httpClient.PutAsJsonAsync(
            $"/api/conversations/{conversationId}/memory",
            new UpsertConversationMemoryRequest(summary, preferencesJson),
            cancellationToken);

        return await ReadDataAsync<ConversationMemoryDto>(response, cancellationToken);
    }

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<T>>(cancellationToken);
        if (!response.IsSuccessStatusCode || payload is null || !payload.Success || payload.Data is null)
        {
            throw new InvalidOperationException(payload?.Error ?? $"Context API call failed ({response.StatusCode}).");
        }

        return payload.Data;
    }
}
