namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// A single <c>claude</c> subprocess invocation: the resolved executable plus its
/// arguments, optional stdin, working directory, environment overrides and timeout.
/// </summary>
internal sealed class ClaudeCliInvocation
{
    /// <summary>Resolved absolute path to the <c>claude</c> executable (see <see cref="ClaudeCliLocator"/>).</summary>
    public required string ExecutablePath { get; init; }

    /// <summary>Command-line arguments (each element is one argv entry — no manual quoting).</summary>
    public required IReadOnlyList<string> Arguments { get; init; }

    /// <summary>Optional text written to the process's stdin, then closed.</summary>
    public string? StandardInput { get; init; }

    /// <summary>Working directory; null = the current process directory.</summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>
    /// Environment variables layered onto the inherited environment. A <c>null</c>
    /// value removes the variable. The ambient environment (including the user's
    /// <c>claude login</c> credentials) is always inherited.
    /// </summary>
    public IReadOnlyDictionary<string, string?>? EnvironmentOverrides { get; init; }

    /// <summary>Hard wall-clock timeout; on expiry the process tree is killed.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(30);
}

/// <summary>Result of a non-streaming <c>claude</c> invocation.</summary>
internal sealed class ClaudeProcessResult
{
    public int ExitCode { get; init; }
    public string StandardOutput { get; init; } = "";
    public string StandardError { get; init; } = "";
}

/// <summary>
/// Runs the <c>claude</c> CLI as a subprocess. Abstracted from
/// <see cref="AnthropicCliTransport"/> so the transport can be unit-tested with a fake
/// runner — no real process required at build or test time.
/// </summary>
internal interface IClaudeCliRunner
{
    /// <summary>Runs to completion and returns stdout/stderr/exit code (for <c>--output-format json</c>).</summary>
    Task<ClaudeProcessResult> RunAsync(ClaudeCliInvocation invocation, CancellationToken cancellationToken);

    /// <summary>
    /// Runs and yields stdout lines as they arrive (for <c>--output-format stream-json</c>).
    /// Throws if the process exits non-zero.
    /// </summary>
    IAsyncEnumerable<string> StreamLinesAsync(ClaudeCliInvocation invocation, CancellationToken cancellationToken);
}
