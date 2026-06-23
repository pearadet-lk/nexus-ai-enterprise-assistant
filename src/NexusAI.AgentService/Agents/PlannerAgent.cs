using System.Text.Json;
using Microsoft.SemanticKernel;
using NexusAI.Contracts.Agents;
using NexusAI.Contracts.Mcp;

namespace NexusAI.AgentService.Agents;

public sealed class PlannerAgent(Kernel kernel)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<AgentPlan> CreatePlanAsync(
        string userMessage,
        string memoryContext,
        IReadOnlyList<McpToolDto> tools,
        CancellationToken cancellationToken)
    {
        var toolCatalog = string.Join(
            Environment.NewLine,
            tools.Select(tool => $"- {tool.ServerId}/{tool.Name}: {tool.Description}"));

        var systemPrompt = """
            You are the NexusAI Planner Agent.
            Break the user request into ordered steps for an enterprise assistant pipeline.
            Return ONLY valid JSON with this shape:
            {
              "steps": [
                {
                  "order": 1,
                  "title": "short step title",
                  "description": "what this step does",
                  "toolServerId": "sql or files or null",
                  "toolName": "mcp tool name or null",
                  "arguments": { "key": "value" } or null
                }
              ]
            }
            Rules:
            - Use MCP tools when data or documents are needed.
            - Always end with a synthesis step (no tool) to prepare the final answer.
            - Prefer get_delayed_shipments for Thailand shipment delay questions.
            - Keep 2-4 steps.
            """;

        var userPrompt = $"""
            User request:
            {userMessage}

            Memory context:
            {memoryContext}

            Available MCP tools:
            {toolCatalog}
            """;

        var raw = await AgentLlmHelper.CompleteAsync(kernel, systemPrompt, userPrompt, cancellationToken);
        var plan = ParsePlan(raw);

        if (plan.Steps.Count == 0)
        {
            plan = new AgentPlan(
            [
                new AgentPlanStep(1, "Answer request", "Respond directly to the user", null, null, null)
            ]);
        }

        return plan;
    }

    private static AgentPlan ParsePlan(string raw)
    {
        try
        {
            var jsonStart = raw.IndexOf('{');
            var jsonEnd = raw.LastIndexOf('}');
            if (jsonStart < 0 || jsonEnd <= jsonStart)
            {
                return new AgentPlan([]);
            }

            var json = raw[jsonStart..(jsonEnd + 1)];
            var parsed = JsonSerializer.Deserialize<PlannerResponse>(json, JsonOptions);
            if (parsed?.Steps is null || parsed.Steps.Count == 0)
            {
                return new AgentPlan([]);
            }

            var steps = parsed.Steps
                .OrderBy(step => step.Order)
                .Select(step => new AgentPlanStep(
                    step.Order,
                    step.Title ?? $"Step {step.Order}",
                    step.Description,
                    step.ToolServerId,
                    step.ToolName,
                    step.Arguments))
                .ToList();

            return new AgentPlan(steps);
        }
        catch
        {
            return new AgentPlan([]);
        }
    }

    private sealed class PlannerResponse
    {
        public List<PlannerStep> Steps { get; set; } = [];
    }

    private sealed class PlannerStep
    {
        public int Order { get; set; }

        public string? Title { get; set; }

        public string? Description { get; set; }

        public string? ToolServerId { get; set; }

        public string? ToolName { get; set; }

        public Dictionary<string, object?>? Arguments { get; set; }
    }
}
