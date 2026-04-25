using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Zonit.Extensions.Ai;

/// <summary>
/// <see cref="ITool"/> adapter that proxies invocations to a tool on an MCP
/// server. Exposed to the agent under the name <c>"{server}.{tool}"</c>.
/// </summary>
[RequiresUnreferencedCode("JSON serialization requires types that cannot be statically analyzed.")]
[RequiresDynamicCode("JSON serialization requires runtime code generation.")]
internal sealed class McpTool : ITool
{
    private readonly McpClient _client;
    private readonly McpToolDescriptor _descriptor;

    public McpTool(McpClient client, McpToolDescriptor descriptor, string? namePrefix)
    {
        _client = client;
        _descriptor = descriptor;

        // Prefix ensures tools from different servers don't collide and the
        // audit trail (ToolInvocation.McpServer / Name) is unambiguous.
        Name = string.IsNullOrEmpty(namePrefix)
            ? descriptor.Name
            : $"{namePrefix}.{descriptor.Name}";

        // Underlying name the server expects — used for tools/call payload.
        RemoteName = descriptor.Name;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <summary>Name as exposed by the MCP server (without prefix).</summary>
    public string RemoteName { get; }

    /// <inheritdoc />
    public string Description => _descriptor.Description;

    /// <inheritdoc />
    public JsonElement InputSchema => _descriptor.InputSchema;

    /// <inheritdoc />
    public Task<JsonElement> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken)
        => _client.CallToolAsync(RemoteName, arguments, cancellationToken);
}
