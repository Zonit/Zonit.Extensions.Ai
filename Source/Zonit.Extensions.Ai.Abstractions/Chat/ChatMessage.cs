namespace Zonit.Extensions.Ai;

/// <summary>
/// Base type for messages in a chat conversation passed to <c>IAiProvider.ChatAsync</c>.
/// Concrete types: <see cref="User"/>, <see cref="Assistant"/>, <see cref="Tool"/>.
/// </summary>
/// <remarks>
/// Use the records directly:
/// <code>
/// var chat = new ChatMessage[]
/// {
///     new User("Hi"),
///     new Assistant("Hello, how can I help?"),
///     new User("What's 2+2?")
/// };
/// </code>
/// <see cref="Tool"/> is produced by the runtime (not constructed by the developer)
/// to record tool/function-call results that flowed through the conversation.
/// </remarks>
public abstract record ChatMessage;
