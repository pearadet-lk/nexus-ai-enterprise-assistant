using Microsoft.SemanticKernel;
using NexusAI.AgentService.Services;
using NexusAI.Contracts.Agents;
using NexusAI.Contracts.Conversations;

namespace NexusAI.AgentService.Agents;

public sealed class MemoryAgent(Kernel kernel, IContextApiClient contextApi)
{
    public async Task<string> BuildContextAsync(
        Guid conversationId,
        IReadOnlyList<MessageDto> messages,
        string latestUserMessage,
        CancellationToken cancellationToken)
    {
        var memory = await contextApi.GetMemoryAsync(conversationId, cancellationToken);
        var summary = memory.Summary ?? string.Empty;
        var preferences = memory.PreferencesJson ?? string.Empty;

        if (messages.Count >= 4 || summary.Length == 0)
        {
            var transcript = string.Join(
                Environment.NewLine,
                messages.OrderBy(message => message.CreatedAt)
                    .TakeLast(12)
                    .Select(message => $"{message.Role}: {message.Content}"));

            var systemPrompt = """
                You are the NexusAI Memory Agent.
                Summarize the conversation for future turns in 3-5 bullet points.
                Also extract durable user preferences (format, tone, filters) if present.
                Return plain text with sections:
                SUMMARY:
                - ...
                PREFERENCES:
                - ... (or "none")
                """;

            var userPrompt = $"""
                Latest user message:
                {latestUserMessage}

                Conversation transcript:
                {transcript}
                """;

            var generated = await AgentLlmHelper.CompleteAsync(kernel, systemPrompt, userPrompt, cancellationToken);
            var parsed = ParseMemoryOutput(generated);

            summary = parsed.Summary;
            preferences = parsed.Preferences;

            await contextApi.UpsertMemoryAsync(
                conversationId,
                summary,
                preferences == "none" ? null : preferences,
                cancellationToken);
        }

        return string.Join(
            Environment.NewLine,
            new[]
            {
                string.IsNullOrWhiteSpace(summary) ? null : $"Conversation summary:\n{summary}",
                string.IsNullOrWhiteSpace(preferences) ? null : $"User preferences:\n{preferences}"
            }.Where(section => section is not null));
    }

    private static (string Summary, string Preferences) ParseMemoryOutput(string raw)
    {
        var summary = string.Empty;
        var preferences = string.Empty;
        var section = "summary";

        foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith("SUMMARY:", StringComparison.OrdinalIgnoreCase))
            {
                section = "summary";
                continue;
            }

            if (line.StartsWith("PREFERENCES:", StringComparison.OrdinalIgnoreCase))
            {
                section = "preferences";
                var inline = line["PREFERENCES:".Length..].Trim();
                if (!string.IsNullOrWhiteSpace(inline))
                {
                    preferences = inline;
                }

                continue;
            }

            if (section == "summary")
            {
                summary += (summary.Length > 0 ? "\n" : string.Empty) + line;
            }
            else
            {
                preferences += (preferences.Length > 0 ? "\n" : string.Empty) + line;
            }
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            summary = raw.Trim();
        }

        return (summary.Trim(), preferences.Trim());
    }
}
