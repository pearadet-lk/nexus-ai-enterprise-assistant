using System.ComponentModel;
using ModelContextProtocol.Server;

namespace NexusAI.McpServers.Files.Tools;

[McpServerToolType]
public sealed class DocumentTools(IWebHostEnvironment environment, IConfiguration configuration, ILogger<DocumentTools> logger)
{
    [McpServerTool, Description("Reads a document from the enterprise document sandbox.")]
    public async Task<string> ReadDocument(
        [Description("Relative document path, e.g. policies/shipping-delay.md")] string path,
        CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveSafePath(path);
        if (!File.Exists(fullPath))
        {
            return $"Document not found: {path}";
        }

        logger.LogInformation("Reading document {Path}", path);
        return await File.ReadAllTextAsync(fullPath, cancellationToken);
    }

    [McpServerTool, Description("Searches documents in the sandbox for matching text.")]
    public Task<string> SearchDocuments(
        [Description("Text to search for across document contents")] string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult("Search query is required.");
        }

        var root = GetDocumentRoot();
        var matches = new List<string>();
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = File.ReadAllText(file);
            if (content.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(GetRelativePath(root, file));
            }
        }

        if (matches.Count == 0)
        {
            return Task.FromResult($"No documents matched '{query.Trim()}'.");
        }

        return Task.FromResult(string.Join(Environment.NewLine, matches));
    }

    private string ResolveSafePath(string path)
    {
        var root = GetDocumentRoot();
        var combined = Path.GetFullPath(Path.Combine(root, path));
        if (!combined.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Path escapes the document sandbox.");
        }

        return combined;
    }

    private string GetDocumentRoot()
    {
        var configured = configuration["Documents:Root"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured);
        }

        return Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "..", "data", "documents"));
    }

    private static string GetRelativePath(string root, string fullPath) =>
        Path.GetRelativePath(root, fullPath).Replace('\\', '/');
}
