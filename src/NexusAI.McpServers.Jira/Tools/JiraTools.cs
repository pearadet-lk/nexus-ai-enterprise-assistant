using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;
using NexusAI.McpServers.Jira.Services;

namespace NexusAI.McpServers.Jira.Tools;

[McpServerToolType]
public sealed class JiraTools(JiraIssueStore store, ILogger<JiraTools> logger)
{
    [McpServerTool, Description("Creates a Jira incident for logistics or shipment escalations.")]
    public async Task<string> CreateIncident(
        [Description("Short incident title")] string summary,
        [Description("Detailed incident description")] string description,
        [Description("Comma-separated shipment IDs, e.g. SHP-1001,SHP-1002")] string? shipmentIds = null,
        [Description("Priority: Low, Medium, High, Critical")] string priority = "High",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return "Summary is required to create a Jira incident.";
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            return "Description is required to create a Jira incident.";
        }

        var issue = await store.CreateAsync(summary, description, shipmentIds, priority, cancellationToken);
        logger.LogInformation("Jira incident {Key} created for shipments {ShipmentIds}", issue.Key, shipmentIds);

        var builder = new StringBuilder();
        builder.AppendLine($"Created Jira incident {issue.Key}");
        builder.AppendLine($"Summary: {issue.Summary}");
        builder.AppendLine($"Status: {issue.Status}");
        builder.AppendLine($"Priority: {issue.Priority}");
        if (!string.IsNullOrWhiteSpace(issue.ShipmentIds))
        {
            builder.AppendLine($"Shipment IDs: {issue.ShipmentIds}");
        }

        builder.AppendLine($"URL: https://jira.example.local/browse/{issue.Key}");
        return builder.ToString().TrimEnd();
    }

    [McpServerTool, Description("Searches Jira incidents by keyword, shipment ID, or issue key.")]
    public async Task<string> SearchIssues(
        [Description("Search text, shipment ID (e.g. SHP-1001), or issue key (e.g. LOG-1001)")] string query,
        CancellationToken cancellationToken = default)
    {
        var matches = await store.SearchAsync(query, cancellationToken);
        if (matches.Count == 0)
        {
            return $"No Jira issues matched '{query.Trim()}'.";
        }

        var builder = new StringBuilder();
        foreach (var issue in matches)
        {
            builder.AppendLine($"- {issue.Key} [{issue.Status}/{issue.Priority}] {issue.Summary}");
            if (!string.IsNullOrWhiteSpace(issue.ShipmentIds))
            {
                builder.AppendLine($"  Shipments: {issue.ShipmentIds}");
            }
        }

        return builder.ToString().TrimEnd();
    }
}
