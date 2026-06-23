using System.Text.Json;
using Microsoft.SemanticKernel;

namespace NexusAI.AgentService.Agents;

public sealed record ReviewResult(bool Approved, string Feedback);

public sealed class ReviewAgent(Kernel kernel)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<ReviewResult> ReviewAsync(
        string userMessage,
        string draftAnswer,
        string toolEvidence,
        CancellationToken cancellationToken)
    {
        var systemPrompt = """
            You are the NexusAI Review Agent.
            Validate that the draft answer is supported by tool evidence and does not hallucinate.
            Return ONLY JSON:
            { "approved": true/false, "feedback": "short reason" }
            Approve when the answer is consistent with evidence or no tools were needed.
            Reject when facts contradict evidence or unsupported claims appear.
            """;

        var userPrompt = $"""
            User question:
            {userMessage}

            Tool evidence:
            {toolEvidence}

            Draft answer:
            {draftAnswer}
            """;

        var raw = await AgentLlmHelper.CompleteAsync(kernel, systemPrompt, userPrompt, cancellationToken, temperature: 0.1);
        return ParseReview(raw);
    }

    private static ReviewResult ParseReview(string raw)
    {
        try
        {
            var jsonStart = raw.IndexOf('{');
            var jsonEnd = raw.LastIndexOf('}');
            if (jsonStart < 0 || jsonEnd <= jsonStart)
            {
                return new ReviewResult(true, "Review parser fallback approved the answer.");
            }

            var json = raw[jsonStart..(jsonEnd + 1)];
            var parsed = JsonSerializer.Deserialize<ReviewResponse>(json, JsonOptions);
            if (parsed is null)
            {
                return new ReviewResult(true, "Review parser fallback approved the answer.");
            }

            return new ReviewResult(parsed.Approved, parsed.Feedback ?? string.Empty);
        }
        catch
        {
            return new ReviewResult(true, "Review parser fallback approved the answer.");
        }
    }

    private sealed class ReviewResponse
    {
        public bool Approved { get; set; }

        public string? Feedback { get; set; }
    }
}
