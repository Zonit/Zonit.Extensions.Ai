namespace Zonit.Extensions.Ai;

/// <summary>
/// Type-safe, <c>typeof</c>-free way to declare a sub-agent's <see cref="IAgent.Tools"/>.
/// Each generic argument is constrained to <see cref="ITool"/>, so a wrong type is a compile
/// error rather than a runtime surprise.
/// </summary>
/// <example>
/// <code>
/// public override IReadOnlyList&lt;Type&gt; Tools => Toolset.Of&lt;GenerateLinkTool, ContactSaveTool&gt;();
/// </code>
/// </example>
/// <remarks>
/// Returns an <see cref="IReadOnlyList{Type}"/> so it drops straight into <see cref="IAgent.Tools"/>.
/// Need more than six tools (or a dynamic set)? Build the list yourself — the property type is unchanged.
/// </remarks>
public static class Toolset
{
    /// <summary>An empty tool set (the default for an agent that uses no tools).</summary>
    public static IReadOnlyList<Type> None { get; } = [];

    /// <summary>Declares one tool.</summary>
    public static IReadOnlyList<Type> Of<T1>()
        where T1 : class, ITool
        => [typeof(T1)];

    /// <summary>Declares two tools.</summary>
    public static IReadOnlyList<Type> Of<T1, T2>()
        where T1 : class, ITool
        where T2 : class, ITool
        => [typeof(T1), typeof(T2)];

    /// <summary>Declares three tools.</summary>
    public static IReadOnlyList<Type> Of<T1, T2, T3>()
        where T1 : class, ITool
        where T2 : class, ITool
        where T3 : class, ITool
        => [typeof(T1), typeof(T2), typeof(T3)];

    /// <summary>Declares four tools.</summary>
    public static IReadOnlyList<Type> Of<T1, T2, T3, T4>()
        where T1 : class, ITool
        where T2 : class, ITool
        where T3 : class, ITool
        where T4 : class, ITool
        => [typeof(T1), typeof(T2), typeof(T3), typeof(T4)];

    /// <summary>Declares five tools.</summary>
    public static IReadOnlyList<Type> Of<T1, T2, T3, T4, T5>()
        where T1 : class, ITool
        where T2 : class, ITool
        where T3 : class, ITool
        where T4 : class, ITool
        where T5 : class, ITool
        => [typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5)];

    /// <summary>Declares six tools.</summary>
    public static IReadOnlyList<Type> Of<T1, T2, T3, T4, T5, T6>()
        where T1 : class, ITool
        where T2 : class, ITool
        where T3 : class, ITool
        where T4 : class, ITool
        where T5 : class, ITool
        where T6 : class, ITool
        => [typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6)];
}
