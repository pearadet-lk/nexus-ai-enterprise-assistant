using Microsoft.SemanticKernel;
using NexusAI.AgentService.Services;
using NexusAI.Contracts.Agents;
using NexusAI.Contracts.Conversations;

namespace NexusAI.AgentService.Agents;

public interface IAgentPipeline
{
    IAsyncEnumerable<ChatStreamEvent> RunAsync(
        ConversationDto conversation,
        string userMessage,
        CancellationToken cancellationToken);
}

public sealed class AgentPipeline(
    Kernel kernel,
    PlannerAgent planner,
    MemoryAgent memory,
    ToolAgent toolAgent,
    ReviewAgent reviewAgent,
    IMcpGatewayClient mcpGateway,
    ChatRequestContext requestContext) : IAgentPipeline
{
    public async IAsyncEnumerable<ChatStreamEvent> RunAsync(
        ConversationDto conversation,
        string userMessage,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return new ChatStreamEvent("agent", new AgentPhaseEvent("memory", "started", "Loading conversation context"));

        var memoryContext = await memory.BuildContextAsync(
            conversation.Id,
            conversation.Messages,
            userMessage,
            cancellationToken);

        yield return new ChatStreamEvent("agent", new AgentPhaseEvent("memory", "completed", "Memory context ready"));

        yield return new ChatStreamEvent("agent", new AgentPhaseEvent("planner", "started", "Creating execution plan"));

        var tools = await mcpGateway.GetToolsAsync(cancellationToken);
        var plan = await planner.CreatePlanAsync(userMessage, memoryContext, tools, cancellationToken);

        yield return new ChatStreamEvent("plan", plan);
        yield return new ChatStreamEvent("agent", new AgentPhaseEvent("planner", "completed", $"{plan.Steps.Count} steps planned"));

        yield return new ChatStreamEvent("agent", new AgentPhaseEvent("tool", "started", "Executing tool steps"));

        var toolResults = await toolAgent.ExecutePlanAsync(plan, cancellationToken);
        var emittedTools = 0;

        foreach (var step in plan.Steps.Where(step => !string.IsNullOrWhiteSpace(step.ToolName)))
        {
            var result = toolResults.FirstOrDefault(item => item.Order == step.Order);
            yield return new ChatStreamEvent(
                "step",
                new AgentStepEvent(
                    step.Order,
                    step.Title,
                    result?.Status ?? "skipped",
                    result?.Content));
        }

        while (emittedTools < requestContext.ToolEvents.Count)
        {
            yield return new ChatStreamEvent("tool", requestContext.ToolEvents[emittedTools++]);
        }

        yield return new ChatStreamEvent("agent", new AgentPhaseEvent("tool", "completed", "Tool execution finished"));

        var evidence = BuildToolEvidence(toolResults);
        var synthesisPrompt = BuildSynthesisPrompt(userMessage, memoryContext, plan, evidence);

        yield return new ChatStreamEvent("agent", new AgentPhaseEvent("synthesis", "started", "Generating answer"));

        var contentBuilder = new System.Text.StringBuilder();
        await foreach (var delta in AgentLlmHelper.StreamAsync(
            kernel,
            """
            You are NexusAI, an enterprise assistant.
            Produce a clear final answer using the execution plan and tool evidence.
            Do not invent data that is not supported by the evidence.
            """,
            synthesisPrompt,
            cancellationToken))
        {
            contentBuilder.Append(delta);
            yield return new ChatStreamEvent("content", new { delta });
        }

        yield return new ChatStreamEvent("agent", new AgentPhaseEvent("synthesis", "completed", "Draft answer ready"));

        var draftAnswer = contentBuilder.ToString();
        if (string.IsNullOrWhiteSpace(draftAnswer))
        {
            draftAnswer = "I could not produce an answer from the available evidence.";
        }

        yield return new ChatStreamEvent("agent", new AgentPhaseEvent("review", "started", "Validating answer"));

        var review = await reviewAgent.ReviewAsync(userMessage, draftAnswer, evidence, cancellationToken);
        var retried = false;

        if (!review.Approved)
        {
            retried = true;
            yield return new ChatStreamEvent(
                "review",
                new AgentReviewEvent(false, review.Feedback, false));

            yield return new ChatStreamEvent("content_reset", new { reason = review.Feedback });
            contentBuilder.Clear();
            var retryPrompt = $"""
                {synthesisPrompt}

                Reviewer feedback (must fix):
                {review.Feedback}
                """;

            await foreach (var delta in AgentLlmHelper.StreamAsync(
                kernel,
                """
                You are NexusAI. Revise the answer to address reviewer feedback.
                Stay faithful to tool evidence.
                """,
                retryPrompt,
                cancellationToken))
            {
                contentBuilder.Append(delta);
                yield return new ChatStreamEvent("content", new { delta });
            }

            draftAnswer = contentBuilder.ToString();
            review = await reviewAgent.ReviewAsync(userMessage, draftAnswer, evidence, cancellationToken);
        }

        yield return new ChatStreamEvent(
            "review",
            new AgentReviewEvent(review.Approved, review.Feedback, retried));

        yield return new ChatStreamEvent("agent", new AgentPhaseEvent("review", "completed", review.Approved ? "Answer approved" : "Answer sent with review notes"));

        yield return new ChatStreamEvent("answer", new { content = draftAnswer });
    }

    private static string BuildToolEvidence(IReadOnlyList<ToolStepResult> toolResults)
    {
        if (toolResults.Count == 0)
        {
            return "No tool evidence was produced.";
        }

        return string.Join(
            Environment.NewLine + Environment.NewLine,
            toolResults.Select(result => $"Step {result.Order} - {result.Title} ({result.Status}):\n{result.Content}"));
    }

    private static string BuildSynthesisPrompt(
        string userMessage,
        string memoryContext,
        AgentPlan plan,
        string evidence)
    {
        var planText = string.Join(
            Environment.NewLine,
            plan.Steps.Select(step => $"{step.Order}. {step.Title} - {step.Description}"));

        return $"""
            User question:
            {userMessage}

            Memory:
            {memoryContext}

            Plan:
            {planText}

            Tool evidence:
            {evidence}
            """;
    }
}
