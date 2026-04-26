using Zonit.Extensions;

namespace Zonit.Extensions.Ai;

/// <summary>
/// A user-authored message in a chat conversation.
/// </summary>
/// <param name="Text">The user's message text.</param>
/// <param name="Files">
/// Optional per-message attachments (images, documents). Independent from
/// <c>IPrompt.Files</c> on the system prompt — both can be supplied.
/// </param>
public sealed record User(string Text, IReadOnlyList<Asset>? Files = null) : ChatMessage;
