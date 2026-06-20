using System.Diagnostics.CodeAnalysis;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Typed bag of trusted, server-supplied context for an agent run. Values are keyed by type,
/// supplied via the agent/chat call's <c>WithContext(...)</c>, and handed to every tool's
/// <c>ExecuteAsync(IRunContext context, …)</c> — never serialized into the model's prompt, so the
/// model can neither read nor forge them.
/// </summary>
/// <remarks>
/// <para>
/// Mirrors ASP.NET Core's <c>IFeatureCollection</c>: a tool reads only the models it cares about
/// with <see cref="Get{T}"/> / <see cref="GetRequired{T}"/> instead of a single overloaded context
/// object. You can register many models and pick the ones a given tool needs.
/// </para>
/// <para>
/// The bag holds your instances <b>by reference</b>, so a tool may mutate a context model in place
/// (e.g. assign an id it resolved) and later tools — and the host, after the run — observe the
/// change. This keeps server-resolved values (like a worker id) out of the model's token stream
/// entirely. Whether a model is mutable is your design choice: <c>get; set;</c> allows writes,
/// <c>get; init;</c> / get-only makes it read-only. Replace a whole value (records / immutable
/// models) with <see cref="Set{T}"/>.
/// </para>
/// <para>
/// Resolution matches the supplied values by exact runtime type first, then by a single assignable
/// type (interface / base class); more than one assignable value is ambiguous and throws
/// <see cref="AiToolContextException"/>. Reads are null-tolerant (<see cref="Get{T}"/>); use
/// <see cref="GetRequired{T}"/> when the tool cannot run without the value — that throw is a wiring
/// error surfaced to the caller, not reported to the model.
/// </para>
/// </remarks>
public interface IRunContext
{
    /// <summary>Returns the context value of type <typeparamref name="T"/>, or <c>null</c> when none was supplied.</summary>
    T? Get<T>() where T : class;

    /// <summary>
    /// Returns the value of type <typeparamref name="T"/>, throwing <see cref="AiToolContextException"/>
    /// when absent. Mirrors <c>IServiceProvider.GetRequiredService&lt;T&gt;()</c> — use it when the tool
    /// cannot meaningfully run without the value.
    /// </summary>
    T GetRequired<T>() where T : class;

    /// <summary>Tries to get the value of type <typeparamref name="T"/>; returns <c>false</c> (and null) when absent.</summary>
    bool TryGet<T>([NotNullWhen(true)] out T? value) where T : class;

    /// <summary>Whether a value of type <typeparamref name="T"/> is present. Handy in an <c>IAgent.IsAvailable</c> permission gate.</summary>
    bool Has<T>() where T : class;

    /// <summary>
    /// Adds or replaces the value stored under <typeparamref name="T"/>. Use it for immutable models
    /// (records) where in-place mutation is impossible, or to publish a value for later tools to read.
    /// </summary>
    void Set<T>(T value) where T : class;

    /// <summary>A snapshot of all current context values — used to forward context down into sub-agents.</summary>
    IReadOnlyCollection<object> Values { get; }
}
