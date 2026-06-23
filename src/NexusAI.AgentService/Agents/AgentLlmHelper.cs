using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace NexusAI.AgentService.Agents;

internal static class AgentLlmHelper
{
    public static async Task<string> CompleteAsync(
        Kernel kernel,
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken,
        double temperature = 0.2)
    {
        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();
        history.AddSystemMessage(systemPrompt);
        history.AddUserMessage(userPrompt);

        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = temperature
        };

        var response = await chatService.GetChatMessageContentAsync(history, settings, kernel, cancellationToken);
        return response.Content ?? string.Empty;
    }

    public static async IAsyncEnumerable<string> StreamAsync(
        Kernel kernel,
        string systemPrompt,
        string userPrompt,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken,
        double temperature = 0.4)
    {
        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();
        history.AddSystemMessage(systemPrompt);
        history.AddUserMessage(userPrompt);

        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = temperature
        };

        await foreach (var chunk in chatService.GetStreamingChatMessageContentsAsync(
            history,
            settings,
            kernel,
            cancellationToken))
        {
            if (!string.IsNullOrEmpty(chunk.Content))
            {
                yield return chunk.Content;
            }
        }
    }
}
