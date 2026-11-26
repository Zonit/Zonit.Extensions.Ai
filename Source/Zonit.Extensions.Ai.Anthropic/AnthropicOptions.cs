namespace Zonit.Extensions;

/// <summary>
/// Selects how the Anthropic provider sends requests.
/// </summary>
public enum AnthropicTransport
{
    /// <summary>
    /// HTTP Messages API with an <c>x-api-key</c> (the default). Configure
    /// <see cref="AiProviderOptions.ApiKey"/>.
    /// </summary>
    Api = 0,

    /// <summary>
    /// Local Claude Code CLI (<c>claude -p</c>, the Claude Agent SDK) invoked as a
    /// subprocess. Authenticates with the machine's <c>claude login</c> session
    /// (subscription) instead of an API key — no <see cref="AiProviderOptions.ApiKey"/>
    /// required. <b>SDK only — no API fallback</b>: a request the CLI cannot represent
    /// (e.g. image/PDF attachments) throws. Configure via <see cref="AnthropicOptions.Cli"/>.
    /// </summary>
    Sdk = 1,

    /// <summary>
    /// Prefer the CLI, fall back to the HTTP API. A request goes to the CLI
    /// (<see cref="Sdk"/>) first; if the CLI cannot represent it and an
    /// <see cref="AiProviderOptions.ApiKey"/> is configured, it is sent via the API
    /// (<see cref="Api"/>); with no key it throws. Best of both — subscription where
    /// possible, key-backed coverage for the rest.
    /// </summary>
    Auto = 2,
}

/// <summary>
/// Anthropic Claude provider configuration options.
/// </summary>
/// <remarks>
/// Configuration section: <c>"Ai:Anthropic"</c>
/// <para>
/// Example appsettings.json:
/// <code>
/// {
///   "Ai": {
///     "Anthropic": {
///       "ApiKey": "sk-ant-...",
///       "BaseUrl": "https://api.anthropic.com"
///     }
///   }
/// }
/// </code>
/// </para>
/// <para>
/// To route through the local Claude Code CLI instead of the HTTP API:
/// <code>
/// {
///   "Ai": {
///     "Anthropic": {
///       "Transport": "Sdk",
///       "Cli": { "ExecutablePath": "C:\\Users\\me\\.local\\bin\\claude.exe" }
///     }
///   }
/// }
/// </code>
/// </para>
/// </remarks>
public sealed class AnthropicOptions : AiProviderOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Ai:Anthropic";

    /// <summary>
    /// Which transport carries requests: <see cref="AnthropicTransport.Api"/> (default,
    /// HTTP Messages API with <see cref="AiProviderOptions.ApiKey"/>),
    /// <see cref="AnthropicTransport.Sdk"/> (local <c>claude</c> CLI only), or
    /// <see cref="AnthropicTransport.Auto"/> (CLI first, API fallback when a key is set).
    /// Must be chosen on the first <c>AddAiAnthropic*</c> registration (provider
    /// registration is idempotent — the first call wins).
    /// </summary>
    public AnthropicTransport Transport { get; set; } = AnthropicTransport.Api;

    /// <summary>
    /// Settings for the <see cref="AnthropicTransport.Sdk"/> (Claude Code CLI) transport.
    /// Ignored when <see cref="Transport"/> is <see cref="AnthropicTransport.Api"/>.
    /// Bound from the <c>"Ai:Anthropic:Cli"</c> configuration section.
    /// </summary>
    public AnthropicCliOptions Cli { get; set; } = new();

    // Stream liveness and agent-turn retry are configured once, for all
    // providers, on AiOptions.Resilience ("Ai:Resilience") — see AiResilienceOptions.
}

/// <summary>
/// Settings for the Claude Code CLI transport (<see cref="AnthropicTransport.Sdk"/>).
/// </summary>
/// <remarks>
/// Configuration section: <c>"Ai:Anthropic:Cli"</c>.
/// <para>
/// Authentication defaults to the machine's ambient <c>claude login</c> session, so
/// no token is needed for local/subscription use. Set <see cref="OAuthToken"/> /
/// <see cref="AuthToken"/> (or <see cref="AiProviderOptions.ApiKey"/>) to inject an
/// explicit credential into the child process environment for headless/CI runs.
/// </para>
/// </remarks>
public sealed class AnthropicCliOptions
{
    /// <summary>
    /// Absolute path to the <c>claude</c> executable. When unset, the binary is
    /// auto-discovered per OS (Windows: <c>claude.exe</c>/<c>claude.cmd</c> on PATH,
    /// <c>%APPDATA%\npm</c>, <c>%USERPROFILE%\.local\bin</c>, the Claude Desktop bundle;
    /// Linux/macOS: <c>claude</c> on PATH, <c>~/.local/bin</c>, npm global). When discovery finds
    /// the npm <c>claude.cmd</c> shim, the native <c>claude.exe</c> npm installs next to it is used
    /// instead. Set this when the CLI is installed somewhere non-standard or not on the service
    /// account's PATH.
    /// </summary>
    public string? ExecutablePath { get; set; }

    /// <summary>
    /// Extra command-line arguments appended verbatim to every <c>claude</c>
    /// invocation (e.g. <c>--add-dir</c>, <c>--mcp-config</c>). Use for flags this
    /// transport does not model directly.
    /// </summary>
    public string[]? AdditionalArguments { get; set; }

    /// <summary>
    /// Working directory for the <c>claude</c> process. Affects file-relative
    /// operations and session lookup. When unset: a fresh, empty temporary directory if
    /// <see cref="Isolated"/> is on (the default), otherwise the current process directory.
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Permission mode passed as <c>--permission-mode</c> (e.g. <c>default</c>,
    /// <c>acceptEdits</c>, <c>plan</c>, <c>bypassPermissions</c>). Left unset, the CLI's
    /// own default applies. Free-form string so new CLI modes need no library change.
    /// </summary>
    public string? PermissionMode { get; set; }

    /// <summary>
    /// OAuth token from <c>claude setup-token</c>, injected as the
    /// <c>CLAUDE_CODE_OAUTH_TOKEN</c> environment variable for the child process.
    /// Use for non-interactive subscription auth (CI). Optional — ambient
    /// <c>claude login</c> is used when unset.
    /// </summary>
    public string? OAuthToken { get; set; }

    /// <summary>
    /// Bearer token injected as <c>ANTHROPIC_AUTH_TOKEN</c> for the child process
    /// (e.g. for an LLM gateway/proxy). Optional.
    /// </summary>
    public string? AuthToken { get; set; }

    /// <summary>
    /// Additional environment variables layered onto the child process environment
    /// (after the ambient environment and auth overrides). Optional.
    /// </summary>
    public Dictionary<string, string>? AdditionalEnvironment { get; set; }

    /// <summary>
    /// Keeps the CLI session to what the request carries, the way the HTTP API path behaves.
    /// <c>true</c> by default. When on, every <c>claude</c> invocation:
    /// <list type="bullet">
    ///   <item><description>gets no built-in Claude Code tools (<c>--tools ""</c>) — an agent sees only the
    ///   tools it registered, through the loopback tool bridge;</description></item>
    ///   <item><description>loads no MCP servers besides the bridge (<c>--strict-mcp-config</c>), so the
    ///   signed-in claude.ai account's connectors and user/project MCP servers stay out;</description></item>
    ///   <item><description>loads no user or project settings, <c>CLAUDE.md</c> or memory
    ///   (<c>--setting-sources local</c>), no skills or slash commands, and saves no session;</description></item>
    ///   <item><description>replaces Claude Code's own system prompt with the request's system text
    ///   (<c>--system-prompt-file</c>) instead of appending to it;</description></item>
    ///   <item><description>runs in a fresh, empty working directory unless <see cref="WorkingDirectory"/> is set;</description></item>
    ///   <item><description>drops the variables a parent Claude Code / Agent SDK session hands its children
    ///   (<c>CLAUDECODE</c>, <c>CLAUDE_CODE_*</c>, <c>CLAUDE_AGENT_SDK_*</c>) and <c>ANTHROPIC_API_KEY</c>, so a
    ///   host started inside a Claude Code session behaves like any other and the subscription — not the
    ///   API — is billed. Credentials and routing settings are kept (<c>CLAUDE_CODE_OAUTH_TOKEN</c>,
    ///   <c>CLAUDE_CODE_USE_BEDROCK</c> / <c>_VERTEX</c> / <c>_FOUNDRY</c> and their <c>SKIP_*_AUTH</c>,
    ///   <c>CLAUDE_CODE_GIT_BASH_PATH</c>, <c>CLAUDE_CODE_CLIENT_*</c>); re-add anything else through
    ///   <see cref="AdditionalEnvironment"/>, which is applied last.</description></item>
    /// </list>
    /// Set <c>false</c> to run the full Claude Code session instead (its built-in tools, connectors,
    /// <c>CLAUDE.md</c>, memory and system prompt, with the request's system text appended).
    /// </summary>
    public bool Isolated { get; set; } = true;

    /// <summary>
    /// Hard wall-clock timeout for a single <c>claude</c> invocation. When unset,
    /// falls back to <see cref="AiProviderOptions.Timeout"/>, then to the global
    /// <c>Ai:Resilience:TotalRequestTimeout</c>. On expiry the process tree is killed.
    /// </summary>
    public TimeSpan? Timeout { get; set; }
}
