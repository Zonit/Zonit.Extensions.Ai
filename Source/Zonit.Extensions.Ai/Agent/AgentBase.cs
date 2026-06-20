using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Base class for a chat-driven sub-agent. Analogous to <c>PromptBase&lt;T&gt;</c> / <c>ToolBase&lt;,&gt;</c>:
/// override <see cref="Name"/>, <see cref="Description"/>, <see cref="Llm"/> and <see cref="Prompt"/>
/// (and optionally <see cref="Tools"/>) — the library handles exposing it to a parent and running the
/// nested loop. The parent conversation is forwarded as this sub-agent's history.
/// </summary>
/// <typeparam name="TOutput">Logical output type (the tool-delegation path returns the final text).</typeparam>
public abstract class AgentBase<TOutput> : IAgent<TOutput>
{
    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract string Description { get; }

    /// <inheritdoc />
    public abstract IAgentLlm Llm { get; }

    /// <inheritdoc />
    public abstract string Prompt { get; }

    /// <inheritdoc />
    public virtual IReadOnlyList<Type> Tools => [];

    /// <inheritdoc />
    public virtual IReadOnlyList<Mcp> Mcps => [];

    /// <inheritdoc />
    public virtual bool ForwardChat => true;

    /// <inheritdoc />
    public virtual bool IsAvailable(IRunContext context) => true;
}

/// <summary>
/// Base class for a parametrized sub-agent. The parent model fills <typeparamref name="TInput"/>
/// (its JSON schema is generated automatically, AOT-safe, the same way <c>ToolBase&lt;TInput, _&gt;</c>
/// does); that JSON becomes the sub-agent's task.
/// </summary>
/// <typeparam name="TInput">Input DTO. Schema generated from its public properties and <c>[Description]</c> attributes.</typeparam>
/// <typeparam name="TOutput">Logical output type (the tool-delegation path returns the final text).</typeparam>
public abstract class AgentBase<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TInput,
    TOutput> : IAgent<TInput, TOutput>, IInputAgent
    where TInput : class
{
    // Schema is built once per concrete TInput and cached. Prefers the AOT-safe build-time schema
    // (AiSchemaRegistry); falls back to reflection only for types the generator did not see. TInput
    // carries [DynamicallyAccessedMembers] so the schema call is trim-clean (no false suppressions).
    private static readonly Lazy<JsonElement> _schema = new(
        () => AiSchemaRegistry.GetSchema(typeof(TInput)),
        isThreadSafe: true);

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract string Description { get; }

    /// <inheritdoc />
    public abstract IAgentLlm Llm { get; }

    /// <inheritdoc />
    public abstract string Prompt { get; }

    /// <inheritdoc />
    public virtual IReadOnlyList<Type> Tools => [];

    /// <inheritdoc />
    public virtual IReadOnlyList<Mcp> Mcps => [];

    /// <inheritdoc />
    public virtual bool ForwardChat => true;

    /// <inheritdoc />
    public virtual bool IsAvailable(IRunContext context) => true;

    JsonElement IInputAgent.InputSchema => _schema.Value;
}

/// <summary>
/// Internal marker for a sub-agent that takes a model-provided input: exposes the precomputed
/// JSON schema so <see cref="AgentToolAdapter"/> can present it to the parent model without
/// reflecting over an unannotated <see cref="Type"/> (the schema is computed in the generic base,
/// where <c>TInput</c> flows with its trimming annotation).
/// </summary>
internal interface IInputAgent : IAgent
{
    /// <summary>Precomputed JSON schema describing the sub-agent's input parameters.</summary>
    JsonElement InputSchema { get; }
}
