using System.Text.Json.Serialization;

using Microsoft.SemanticKernel;

using NexusAI.AgentService.Agents;

using NexusAI.Contracts.Conversations;

using NexusAI.SharedKernel.Constants;



namespace NexusAI.AgentService.Services;



public interface IChatOrchestrator

{

    IAsyncEnumerable<ChatStreamEvent> StreamChatAsync(

        Guid? conversationId,

        string message,

        string accessToken,

        CancellationToken cancellationToken);

}



public sealed record ChatStreamEvent(

    string Type,

    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] object? Data);



public sealed class ChatOrchestrator(

    IAgentPipeline agentPipeline,

    IContextApiClient contextApi,

    ChatRequestContext requestContext,

    IConfiguration configuration) : IChatOrchestrator

{

    public async IAsyncEnumerable<ChatStreamEvent> StreamChatAsync(

        Guid? conversationId,

        string message,

        string accessToken,

        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)

    {

        requestContext.AccessToken = accessToken;

        requestContext.ToolEvents.Clear();



        ConversationDto conversation;

        if (conversationId is null || conversationId == Guid.Empty)

        {

            var title = message.Length > 48 ? $"{message[..48]}…" : message;

            conversation = await contextApi.CreateConversationAsync(title, cancellationToken);

        }

        else

        {

            conversation = await contextApi.GetConversationAsync(conversationId.Value, cancellationToken);

        }



        requestContext.ConversationId = conversation.Id;

        yield return new ChatStreamEvent("conversation", new { conversationId = conversation.Id });



        await contextApi.AddMessageAsync(conversation.Id, MessageRoles.User, message, cancellationToken);



        var apiKey = configuration["OpenAI:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))

        {

            yield return new ChatStreamEvent("error", new { message = "OpenAI API key is not configured. Set OpenAI:ApiKey in user secrets or environment variables." });

            yield break;

        }



        var finalAnswer = string.Empty;



        await foreach (var streamEvent in agentPipeline.RunAsync(conversation, message, cancellationToken))

        {

            if (streamEvent.Type == "answer" && streamEvent.Data is not null)

            {

                var json = System.Text.Json.JsonSerializer.Serialize(streamEvent.Data);

                var answer = System.Text.Json.JsonSerializer.Deserialize<AnswerPayload>(json);

                if (!string.IsNullOrWhiteSpace(answer?.Content))

                {

                    finalAnswer = answer.Content;

                }

            }



            yield return streamEvent;

        }



        if (string.IsNullOrWhiteSpace(finalAnswer))

        {

            finalAnswer = "I was unable to generate a response. Please try again.";

        }



        var savedMessage = await contextApi.AddMessageAsync(

            conversation.Id,

            MessageRoles.Assistant,

            finalAnswer,

            cancellationToken);



        var promptTokens = EstimateTokens(message, conversation.Messages);

        var completionTokens = EstimateTokens(finalAnswer);

        var cost = CalculateCost(promptTokens, completionTokens);



        await contextApi.LogAuditAsync(

            "agent_pipeline_completion",

            promptTokens,

            completionTokens,

            cost,

            cancellationToken);



        yield return new ChatStreamEvent("done", new

        {

            messageId = savedMessage.Id,

            conversationId = conversation.Id,

            promptTokens,

            completionTokens,

            cost

        });

    }



    private decimal CalculateCost(int promptTokens, int completionTokens)

    {

        var inputRate = configuration.GetValue<decimal>("OpenAI:InputCostPerMillion", 0.15m);

        var outputRate = configuration.GetValue<decimal>("OpenAI:OutputCostPerMillion", 0.60m);

        return (promptTokens * inputRate + completionTokens * outputRate) / 1_000_000m;

    }



    private static int EstimateTokens(string currentMessage, IReadOnlyList<MessageDto>? history = null)

    {

        var length = currentMessage.Length;

        if (history is not null)

        {

            length += history.Sum(x => x.Content.Length);

        }



        return Math.Max(1, length / 4);

    }



    private static int EstimateTokens(string text) => Math.Max(1, text.Length / 4);



    private sealed class AnswerPayload

    {

        public string? Content { get; set; }

    }

}



public static class KernelRegistration

{

    public static IServiceCollection AddNexusKernel(this IServiceCollection services, IConfiguration configuration)

    {

        var apiKey = configuration["OpenAI:ApiKey"] ?? string.Empty;

        var model = configuration["OpenAI:Model"] ?? "gpt-4o-mini";



        var kernelBuilder = services.AddKernel();

        if (!string.IsNullOrWhiteSpace(apiKey))

        {

            kernelBuilder.AddOpenAIChatCompletion(model, apiKey);

        }



        services.AddScoped<PlannerAgent>();

        services.AddScoped<MemoryAgent>();

        services.AddScoped<ToolAgent>();

        services.AddScoped<ReviewAgent>();

        services.AddScoped<IAgentPipeline, AgentPipeline>();

        services.AddScoped<IFunctionInvocationFilter, Filters.ToolExecutionLoggingFilter>();

        services.AddScoped<IChatOrchestrator, ChatOrchestrator>();



        return services;

    }

}


