using System.Diagnostics.CodeAnalysis;

namespace Zonit.Extensions.Ai;

/// <summary>
/// A declarative sub-agent: a named, self-contained agent that a parent agent can delegate to.
/// Conceptually the agent-level counterpart of <see cref="ITool"/> — the parent sees only
/// <see cref="Name"/> and <see cref="Description"/> and decides when to hand work over.
/// </summary>
/// <remarks>
/// <para>
/// A sub-agent runs on its <b>own</b> model (<see cref="Llm"/>) with its <b>own</b> tools
/// (<see cref="Tools"/>) and its <b>own</b> system instruction (<see cref="Prompt"/>), in an
/// isolated loop. When exposed to a parent via <c>IAgentRequest.AddAgent&lt;T&gt;()</c> /
/// <c>IChatRequest.AddAgent&lt;T&gt;()</c> the library wraps it as a tool: invoking it runs the
/// sub-agent and returns its final text to the parent, which can re-voice it (e.g. a cheap
/// router/persona model that translates and polishes a specialist sub-agent's draft).
/// </para>
/// <para>
/// Two shapes — pick by how the parent supplies work:
/// <list type="bullet">
///   <item><description><see cref="IAgent{TOutput}"/> — <b>chat-driven</b>: no model arguments;
///   the parent conversation is forwarded as the sub-agent's history (it reads what is going on).</description></item>
///   <item><description><see cref="IAgent{TInput, TOutput}"/> — <b>parametrized</b>: the parent model
///   fills <c>TInput</c> (a JSON schema is generated from it); that becomes the sub-agent's task.</description></item>
/// </list>
/// Prefer the base classes <c>AgentBase&lt;TOutput&gt;</c> / <c>AgentBase&lt;TInput, TOutput&gt;</c>.
/// </para>
/// <para>
/// Trusted server context supplied to the parent via <c>WithContext(...)</c> is forwarded down to
/// the sub-agent, so the sub-agent's scoped tools (<c>ToolBase&lt;TScope, _, _&gt;</c>) receive it
/// without the parent model ever seeing it.
/// </para>
/// </remarks>
public interface IAgent
{
    /// <summary>Unique name visible to the parent model (used as the delegation function name).</summary>
    string Name { get; }

    /// <summary>Description shown to the parent model — explain what this sub-agent does and when to delegate to it.</summary>
    string Description { get; }

    /// <summary>The agent-capable model this sub-agent runs on. Lets you route specialised work to a fitting (cheaper / stronger) model.</summary>
    IAgentLlm Llm { get; }

    /// <summary>System instruction for the sub-agent (its "job"). Plain text; not Scriban-rendered on this path.</summary>
    string Prompt { get; }

    /// <summary>
    /// Tool types available <i>only</i> inside this sub-agent, resolved from DI when it runs.
    /// Empty by default. Declare them <c>typeof</c>-free with <see cref="Toolset"/>, e.g.
    /// <c>Toolset.Of&lt;GenerateLinkTool, ContactSaveTool&gt;()</c> or the unbounded chain
    /// <c>Toolset.Add&lt;GenerateLinkTool&gt;().Add&lt;ContactSaveTool&gt;()</c>. Register each with
    /// <c>AddAiTools&lt;T&gt;()</c> so it is DI-resolvable.
    /// </summary>
    IReadOnlyList<Type> Tools { get; }

    /// <summary>
    /// MCP servers attached <i>only</i> inside this sub-agent, alongside its own <see cref="Tools"/>.
    /// Empty by default. Each <see cref="Mcp"/> is connected when the sub-agent runs and its remote
    /// tools are exposed to the sub-agent's model under the <c>"{Name}.{tool}"</c> prefix (filtered by
    /// <see cref="Mcp.AllowedTools"/>). Declare them with a collection expression, e.g.
    /// <c>public override IReadOnlyList&lt;Mcp&gt; Mcps =&gt; [new("github", "https://mcp.example.com/sse", token)];</c>.
    /// The parent's MCP servers are <b>not</b> inherited — a sub-agent only sees the servers it declares here.
    /// </summary>
    IReadOnlyList<Mcp> Mcps { get; }

    /// <summary>
    /// When the parent was started as a <b>chat</b> (<c>ai.Chat(...).AddAgent&lt;T&gt;()</c>), forward that
    /// conversation to this sub-agent as its history. <c>true</c> by default — and it applies to <b>both</b>
    /// modes, so a parametrized agent still sees the conversation alongside its rendered task. Set to
    /// <c>false</c> to run this sub-agent isolated from the conversation. A parent started as a plain agent
    /// run (<c>ai.Agent(...)</c>) has no conversation, so nothing is forwarded regardless of this flag.
    /// </summary>
    bool ForwardChat { get; }
}

/// <summary>
/// A chat-driven sub-agent: it receives the parent conversation (no model-provided arguments) and
/// produces <typeparamref name="TOutput"/>. See <see cref="IAgent"/>.
/// </summary>
/// <typeparam name="TOutput">Logical output type. On the tool-delegation path the parent receives the final text.</typeparam>
public interface IAgent<TOutput> : IAgent
{
}

/// <summary>
/// A parametrized sub-agent: the parent model supplies <typeparamref name="TInput"/> (schema generated
/// from its public properties), which becomes the sub-agent's task. See <see cref="IAgent"/>.
/// </summary>
/// <typeparam name="TInput">Input DTO filled by the parent model. Schema generated from its public properties and <c>[Description]</c> attributes.</typeparam>
/// <typeparam name="TOutput">Logical output type. On the tool-delegation path the parent receives the final text.</typeparam>
public interface IAgent<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TInput, TOutput> : IAgent
    where TInput : class
{
}
