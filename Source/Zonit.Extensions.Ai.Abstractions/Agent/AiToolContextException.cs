namespace Zonit.Extensions.Ai;

/// <summary>
/// Thrown to the <b>caller</b> (the developer) — never surfaced to the model — for a context
/// wiring mistake: a tool called <see cref="IRunContext.GetRequired{T}"/> for a value that was not
/// supplied to the agent run, or a lookup matched more than one assignable value (ambiguous).
/// </summary>
/// <remarks>
/// A missing required context is a wiring mistake (the tool needs server data you forgot to pass),
/// analogous to a missing DI registration — the model cannot recover from it, so it fails fast to
/// you instead of being reported as a tool error the model would try to work around. Supply the
/// value via the agent/chat call's <c>WithContext(...)</c> argument (call it once per distinct type),
/// or read it with <see cref="IRunContext.Get{T}"/> when its absence is acceptable.
/// </remarks>
public sealed class AiToolContextException : InvalidOperationException
{
    /// <summary>Creates the exception with an explanatory message.</summary>
    public AiToolContextException(string message) : base(message) { }
}
