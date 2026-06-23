using Microsoft.Extensions.Logging;
using NexusAI.Contracts.Messaging;

namespace NexusAI.AuditService;

public sealed class AuditEventProcessor(ILogger<AuditEventProcessor> logger)
{
    public Task ProcessAsync(AuditLoggedMessage message, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Processed audit event {AuditId} action={Action} tokens={Prompt}+{Completion} cost={Cost}",
            message.AuditId,
            message.Action,
            message.PromptTokens,
            message.CompletionTokens,
            message.Cost);

        return Task.CompletedTask;
    }
}
