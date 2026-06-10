namespace Zonit.Extensions.Ai;

/// <summary>
/// Thrown to the <b>caller</b> (the developer) — never surfaced to the model — when a
/// scoped tool (<c>ToolBase&lt;TScope, TInput, TOutput&gt;</c>) runs but the required
/// <c>TScope</c> context was not supplied to the agent call, or more than one supplied
/// value matches its type.
/// </summary>
/// <remarks>
/// A missing context is a wiring mistake (you exposed a scoped tool but forgot to pass
/// <c>context:</c>), analogous to a missing DI registration — the model cannot recover
/// from it, so it fails fast to you instead of being reported as a tool error the model
/// would try to work around. Pass the value via the agent call's <c>context:</c> argument
/// (e.g. <c>context: [user]</c>, or <c>context: [user, billing]</c> for several tools).
/// </remarks>
public sealed class AiToolContextException : InvalidOperationException
{
    /// <summary>Creates the exception with an explanatory message.</summary>
    public AiToolContextException(string message) : base(message) { }
}
