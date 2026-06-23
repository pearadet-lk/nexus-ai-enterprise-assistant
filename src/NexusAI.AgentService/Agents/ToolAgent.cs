using NexusAI.AgentService.Services;
using NexusAI.Contracts.Agents;
using NexusAI.Contracts.Mcp;

namespace NexusAI.AgentService.Agents;

public sealed record ToolStepResult(int Order, string Title, string Content, int DurationMs, string Status);

public sealed class ToolAgent(IMcpGatewayClient mcpGateway, IContextApiClient contextApi, ChatRequestContext requestContext)
{
    public async Task<IReadOnlyList<ToolStepResult>> ExecutePlanAsync(
        AgentPlan plan,
        CancellationToken cancellationToken)
    {
        var results = new List<ToolStepResult>();

        foreach (var step in plan.Steps.Where(step => !string.IsNullOrWhiteSpace(step.ToolName)))
        {
            var toolName = step.ToolName!;
            var serverId = step.ToolServerId ?? await ResolveServerIdAsync(toolName, cancellationToken);
            if (serverId is null)
            {
                results.Add(new ToolStepResult(step.Order, step.Title, $"Tool '{toolName}' is not registered.", 0, "failed"));
                continue;
            }

            try
            {
                var execution = await mcpGateway.ExecuteAsync(
                    serverId,
                    toolName,
                    step.Arguments ?? new Dictionary<string, object?>(),
                    cancellationToken);

                await contextApi.LogToolExecutionAsync(
                    requestContext.ConversationId,
                    toolName,
                    execution.DurationMs,
                    "success",
                    null,
                    cancellationToken);

                requestContext.ToolEvents.Add(new ToolStreamEvent(toolName, execution.DurationMs, "success", null));
                results.Add(new ToolStepResult(step.Order, step.Title, execution.Content, execution.DurationMs, "success"));
            }
            catch (Exception ex)
            {
                await contextApi.LogToolExecutionAsync(
                    requestContext.ConversationId,
                    toolName,
                    0,
                    "failed",
                    ex.Message,
                    cancellationToken);

                requestContext.ToolEvents.Add(new ToolStreamEvent(toolName, 0, "failed", ex.Message));
                results.Add(new ToolStepResult(step.Order, step.Title, ex.Message, 0, "failed"));
            }
        }

        return results;
    }

    private async Task<string?> ResolveServerIdAsync(string toolName, CancellationToken cancellationToken)
    {
        var tools = await mcpGateway.GetToolsAsync(cancellationToken);
        return tools.FirstOrDefault(tool => tool.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase))?.ServerId;
    }
}
