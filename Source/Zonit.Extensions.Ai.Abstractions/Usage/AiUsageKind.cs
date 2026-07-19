namespace Zonit.Extensions.Ai;

/// <summary>
/// Classifies a node in the AI call tree captured by <see cref="IAiUsageTracker"/>.
/// </summary>
/// <remarks>
/// <para>
/// The tree alternates between grouping nodes and model-call nodes:
/// <c>Agent</c> → <c>Tool</c> → (<c>Agent</c> | <c>Chat</c> | <c>Generate</c> | …) → …
/// </para>
/// <list type="bullet">
///   <item><description><see cref="Agent"/> — an agent loop. Its own <c>Usage</c> is the sum of its model turns; nested calls hang off it as children.</description></item>
///   <item><description><see cref="Tool"/> — a single tool invocation. A pure <b>grouping</b> node: it has no usage of its own; its children are the AI calls the tool made.</description></item>
///   <item><description>All other values — a single non-agent model call (one provider round-trip) whose own <c>Usage</c> is the tokens/cost it consumed.</description></item>
/// </list>
/// </remarks>
public enum AiUsageKind
{
    /// <summary>An agent loop (one or more model turns plus tool executions).</summary>
    Agent,

    /// <summary>A single tool invocation — grouping node, no own usage.</summary>
    Tool,

    /// <summary>A single-shot text/structured generation (<c>GenerateAsync(ILlm, …)</c>).</summary>
    Generate,

    /// <summary>A single-shot chat completion without tools (<c>ChatAsync(ILlm, …)</c>).</summary>
    Chat,

    /// <summary>An embedding request.</summary>
    Embed,

    /// <summary>An image generation request.</summary>
    Image,

    /// <summary>An audio transcription request.</summary>
    Audio,

    /// <summary>A speech-synthesis (text-to-speech) request.</summary>
    Speech,

    /// <summary>A video generation request.</summary>
    Video,
}
