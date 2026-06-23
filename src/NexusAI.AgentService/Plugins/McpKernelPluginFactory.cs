using System.Text.Json;
using Microsoft.SemanticKernel;
using NexusAI.AgentService.Services;
using NexusAI.Contracts.Mcp;

namespace NexusAI.AgentService.Plugins;

public static class McpKernelPluginFactory
{
    public static KernelPlugin Create(string pluginName, IReadOnlyList<McpToolDto> tools, IMcpGatewayClient gateway)
    {
        var functions = tools.Select(tool => CreateFunction(tool, gateway)).ToList();
        return KernelPluginFactory.CreateFromFunctions(pluginName, functions);
    }

    private static KernelFunction CreateFunction(McpToolDto tool, IMcpGatewayClient gateway)
    {
        var parameters = ParseParameters(tool.InputSchemaJson);

        return KernelFunctionFactory.CreateFromMethod(
            async (KernelArguments args, CancellationToken cancellationToken) =>
            {
                var arguments = BuildArguments(args, parameters);
                var result = await gateway.ExecuteAsync(
                    tool.ServerId,
                    tool.Name,
                    arguments,
                    cancellationToken);

                return result.Content;
            },
            functionName: tool.Name,
            description: tool.Description,
            parameters: parameters);
    }

    private static Dictionary<string, object?> BuildArguments(
        KernelArguments args,
        IReadOnlyList<KernelParameterMetadata> parameters)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in parameters)
        {
            if (args.TryGetValue(parameter.Name!, out var value) && value is not null)
            {
                result[parameter.Name!] = value;
            }
        }

        return result;
    }

    private static IReadOnlyList<KernelParameterMetadata> ParseParameters(string? inputSchemaJson)
    {
        if (string.IsNullOrWhiteSpace(inputSchemaJson))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(inputSchemaJson);
            if (!document.RootElement.TryGetProperty("properties", out var properties))
            {
                return [];
            }

            var required = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (document.RootElement.TryGetProperty("required", out var requiredNode))
            {
                foreach (var item in requiredNode.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        required.Add(item.GetString()!);
                    }
                }
            }

            var parameters = new List<KernelParameterMetadata>();
            foreach (var property in properties.EnumerateObject())
            {
                var description = property.Value.TryGetProperty("description", out var descNode)
                    ? descNode.GetString()
                    : null;

                parameters.Add(new KernelParameterMetadata(property.Name)
                {
                    Description = description,
                    IsRequired = required.Contains(property.Name),
                    ParameterType = typeof(string),
                    Schema = KernelJsonSchema.Parse(property.Value.GetRawText())
                });
            }

            return parameters;
        }
        catch
        {
            return [];
        }
    }
}
