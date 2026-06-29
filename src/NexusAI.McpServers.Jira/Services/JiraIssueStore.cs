using System.Text.Json;

namespace NexusAI.McpServers.Jira.Services;

public sealed class JiraIssueStore(IConfiguration configuration, IWebHostEnvironment environment, ILogger<JiraIssueStore> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<JiraIssue> CreateAsync(
        string summary,
        string description,
        string? shipmentIds,
        string priority,
        CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var issues = await LoadAsync(cancellationToken);
            var projectKey = configuration["Jira:ProjectKey"] ?? "LOG";
            var nextNumber = issues.Count == 0
                ? 1001
                : issues.Max(issue => ParseIssueNumber(issue.Key)) + 1;

            var issue = new JiraIssue(
                $"{projectKey}-{nextNumber}",
                summary.Trim(),
                description.Trim(),
                "Open",
                NormalizePriority(priority),
                shipmentIds?.Trim(),
                DateTime.UtcNow);

            issues.Add(issue);
            await SaveAsync(issues, cancellationToken);
            logger.LogInformation("Created Jira issue {Key}", issue.Key);
            return issue;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<JiraIssue>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var issues = await LoadAsync(cancellationToken);
        return issues
            .Where(issue =>
                issue.Key.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                issue.Summary.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                issue.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (issue.ShipmentIds?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderByDescending(issue => issue.CreatedAt)
            .ToList();
    }

    private async Task<List<JiraIssue>> LoadAsync(CancellationToken cancellationToken)
    {
        var path = GetStorePath();
        if (!File.Exists(path))
        {
            return [];
        }

        await using var stream = File.OpenRead(path);
        var issues = await JsonSerializer.DeserializeAsync<List<JiraIssue>>(stream, cancellationToken: cancellationToken);
        return issues ?? [];
    }

    private async Task SaveAsync(List<JiraIssue> issues, CancellationToken cancellationToken)
    {
        var path = GetStorePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, issues, JsonOptions, cancellationToken);
    }

    private string GetStorePath()
    {
        var configured = configuration["Jira:StorePath"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured);
        }

        return Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "..", "data", "jira", "issues.json"));
    }

    private static int ParseIssueNumber(string key)
    {
        var dash = key.LastIndexOf('-');
        return dash >= 0 && int.TryParse(key[(dash + 1)..], out var number) ? number : 0;
    }

    private static string NormalizePriority(string priority) =>
        string.IsNullOrWhiteSpace(priority) ? "Medium" : priority.Trim();
}

public sealed record JiraIssue(
    string Key,
    string Summary,
    string Description,
    string Status,
    string Priority,
    string? ShipmentIds,
    DateTime CreatedAt);
