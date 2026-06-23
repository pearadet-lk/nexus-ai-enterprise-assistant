using System.Diagnostics;
using Microsoft.SemanticKernel;
using NexusAI.AgentService.Services;

namespace NexusAI.AgentService.Filters;

public sealed class ToolExecutionLoggingFilter(
    IContextApiClient contextApi,
    ChatRequestContext requestContext) : IFunctionInvocationFilter
{
    public async Task OnFunctionInvocationAsync(
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, Task> next)
    {
        var toolName = context.Function.Name;
        var sw = Stopwatch.StartNew();
        var cancellationToken = context.CancellationToken;

        try
        {
            await next(context);
            sw.Stop();

            await contextApi.LogToolExecutionAsync(
                requestContext.ConversationId,
                toolName,
                (int)sw.ElapsedMilliseconds,
                "success",
                null,
                cancellationToken);

            requestContext.ToolEvents.Add(new ToolStreamEvent(toolName, (int)sw.ElapsedMilliseconds, "success", null));
        }
        catch (Exception ex)
        {
            sw.Stop();

            await contextApi.LogToolExecutionAsync(
                requestContext.ConversationId,
                toolName,
                (int)sw.ElapsedMilliseconds,
                "failed",
                ex.Message,
                cancellationToken);

            requestContext.ToolEvents.Add(new ToolStreamEvent(toolName, (int)sw.ElapsedMilliseconds, "failed", ex.Message));

            throw;
        }
    }
}
