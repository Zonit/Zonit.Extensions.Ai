namespace Zonit.Extensions.Ai;

/// <summary>
/// Describes an external Model Context Protocol (MCP) server the agent
/// should connect to over HTTP / SSE.
/// </summary>
/// <remarks>
/// <para>
/// The library is a <b>client</b> of external MCP servers — we do not host
/// our own MCP server. The value type is intentionally tiny so it can be
/// passed directly to <c>IAiProvider.GenerateAsync</c> or registered in DI
/// via <c>AddAiMcp(...)</c>.
/// </para>
/// <para>
/// Tools exposed by an MCP server are automatically presented to the model
/// under the name <c>"{Name}.{tool}"</c> (or <c>"{Name}__{tool}"</c> when a
/// provider does not accept dots in function names). <see cref="AllowedTools"/>
/// can be used as a whitelist filter — <c>null</c> means "expose every tool
/// the server reports".
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var mcp = new Mcp(
///     name:  "gold",
///     url:   "https://mcp.example.com/sse",
///     token: bearer,
///     allowedTools: new[] { "get_gold_price", "get_cot_data" });
///
/// var result = await provider.GenerateAsync(new GPT5(), prompt, mcps: [mcp]);
/// </code>
/// </example>
public sealed record Mcp
{
    /// <summary>
    /// Unique label of the MCP server. Used as a prefix for tool names exposed
    /// to the model. Must be non-empty and unique within a single agent invocation.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// HTTPS endpoint of the MCP server (typically an SSE / Streamable HTTP endpoint).
    /// </summary>
    public string Url { get; }

    /// <summary>
    /// Optional bearer token sent as <c>Authorization: Bearer {Token}</c>.
    /// <c>null</c> means no authorization header is attached.
    /// </summary>
    public string? Token { get; }

    /// <summary>
    /// Optional whitelist of remote tool names (without the server prefix).
    /// <c>null</c> = expose every tool the server reports. Empty list = expose
    /// no tools (effectively disables the server while keeping it registered).
    /// </summary>
    public IReadOnlyList<string>? AllowedTools { get; }

    /// <summary>
    /// Creates a new MCP client descriptor.
    /// </summary>
    /// <param name="name">Unique label, used as a tool-name prefix (e.g. <c>"github"</c>).</param>
    /// <param name="url">HTTPS endpoint URL.</param>
    /// <param name="token">Optional bearer token.</param>
    /// <param name="allowedTools">
    /// Optional whitelist of remote tool names. <c>null</c> exposes every tool.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="url"/> or <paramref name="name"/> is empty,
    /// or when <paramref name="url"/> is not an absolute HTTPS URL.
    /// </exception>
    public Mcp(
        string name,
        string url,
        string? token = null,
        IReadOnlyList<string>? allowedTools = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("MCP name must be provided.", nameof(name));
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("MCP URL must be provided.", nameof(url));
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed)
            || parsed.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("MCP URL must be an absolute HTTPS URL.", nameof(url));
        }

        Name = name;
        Url = url;
        Token = token;
        AllowedTools = allowedTools;
    }
}
