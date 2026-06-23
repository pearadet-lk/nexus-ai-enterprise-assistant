using System.ComponentModel;
using System.Text;
using Microsoft.Data.SqlClient;
using ModelContextProtocol.Server;

namespace NexusAI.McpServers.Sql.Tools;

[McpServerToolType]
public sealed class ShipmentTools(IConfiguration configuration)
{
    private static readonly HashSet<string> AllowedTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "Shipments"
    };

    [McpServerTool, Description("Returns delayed shipments, optionally filtered by origin country.")]
    public async Task<string> GetDelayedShipments(
        [Description("Filter by origin country, e.g. Thailand")] string? country = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var sql = new StringBuilder("""
            SELECT ShipmentId, Origin, Destination, Status, DelayDays, Eta
            FROM Shipments
            WHERE Status = 'Delayed'
            """);

        await using var command = connection.CreateCommand();
        if (!string.IsNullOrWhiteSpace(country))
        {
            sql.Append(" AND Origin = @country");
            command.Parameters.Add(new SqlParameter("@country", country.Trim()));
        }

        sql.Append(" ORDER BY DelayDays DESC");
        command.CommandText = sql.ToString();

        return await ExecuteReaderAsMarkdownAsync(command, cancellationToken);
    }

    [McpServerTool, Description("Executes a read-only SELECT query against allowlisted tables (Shipments only).")]
    public async Task<string> ExecuteReadOnlyQuery(
        [Description("A SELECT statement. Only the Shipments table is allowed.")] string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return "Query is required.";
        }

        var normalized = query.Trim();
        if (!normalized.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            return "Only SELECT queries are permitted.";
        }

        if (normalized.Contains(';', StringComparison.Ordinal))
        {
            return "Multiple statements are not permitted.";
        }

        var forbidden = new[] { "INSERT", "UPDATE", "DELETE", "DROP", "ALTER", "TRUNCATE", "EXEC", "MERGE" };
        if (forbidden.Any(keyword => normalized.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
        {
            return "Only read-only SELECT queries are permitted.";
        }

        if (!AllowedTables.Any(table => normalized.Contains(table, StringComparison.OrdinalIgnoreCase)))
        {
            return $"Query must reference an allowlisted table: {string.Join(", ", AllowedTables)}.";
        }

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = normalized;

        return await ExecuteReaderAsMarkdownAsync(command, cancellationToken);
    }

    private SqlConnection CreateConnection()
    {
        var connectionString = configuration.GetConnectionString("NexusDb")
            ?? throw new InvalidOperationException("Connection string 'NexusDb' is not configured.");

        return new SqlConnection(connectionString);
    }

    private static async Task<string> ExecuteReaderAsMarkdownAsync(
        SqlCommand command,
        CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!reader.HasRows)
        {
            return "No rows returned.";
        }

        var columns = Enumerable.Range(0, reader.FieldCount)
            .Select(reader.GetName)
            .ToArray();

        var builder = new StringBuilder();
        builder.Append("| ").AppendJoin(" | ", columns).AppendLine(" |");
        builder.Append("| ").AppendJoin(" | ", columns.Select(_ => "---")).AppendLine(" |");

        while (await reader.ReadAsync(cancellationToken))
        {
            var values = new string[columns.Length];
            for (var i = 0; i < columns.Length; i++)
            {
                values[i] = reader.IsDBNull(i) ? string.Empty : reader.GetValue(i)?.ToString() ?? string.Empty;
            }

            builder.Append("| ").AppendJoin(" | ", values).AppendLine(" |");
        }

        return builder.ToString();
    }
}
