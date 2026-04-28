using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Zonit.Extensions.Ai;

/// <summary>
/// <see cref="ITool"/> adapter that proxies invocations to a tool on an MCP
/// server. Exposed to the agent under the name <c>"{server}_{tool}"</c>.
/// </summary>
/// <remarks>
/// The separator is an underscore (not a dot) because every supported provider's
/// function-calling schema restricts tool names to <c>^[a-zA-Z0-9_-]{1,128}$</c>
/// (Anthropic) or stricter equivalents (OpenAI, xAI, Google). Any stray '.' that
/// an MCP server emits in its own tool name is normalized to '_' so the resulting
/// identifier is always provider-safe; <see cref="RemoteName"/> preserves the
/// original spelling for the <c>tools/call</c> payload.
/// </remarks>
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
        var safePrefix = SanitizeIdentifier(namePrefix);
        var safeLocal = SanitizeIdentifier(descriptor.Name) ?? string.Empty;
        Name = string.IsNullOrEmpty(safePrefix)
            ? safeLocal
            : $"{safePrefix}_{safeLocal}";

        // Underlying name the server expects — used for tools/call payload.
        RemoteName = descriptor.Name;
    }

    /// <summary>
    /// Replaces characters that fall outside <c>[a-zA-Z0-9_-]</c> with '_' so
    /// the resulting name passes provider-side regex validation.
    /// </summary>
    private static string? SanitizeIdentifier(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        var buffer = new char[value.Length];
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            buffer[i] = (c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_' or '-')
                ? c
                : '_';
        }
        return new string(buffer);
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
