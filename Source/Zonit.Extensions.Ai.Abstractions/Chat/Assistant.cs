namespace Zonit.Extensions.Ai;

/// <summary>
/// An assistant (model-authored) message in a chat conversation.
/// </summary>
/// <param name="Text">The assistant's reply text.</param>
public sealed record Assistant(string Text) : ChatMessage;
