using System.Text;
using Zonit.Extensions;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Internal helper that collapses a <see cref="ChatMessage"/> sequence into a
/// single <see cref="IPrompt{TResponse}"/> so providers without native
/// multi-turn support can still service <c>ChatAsync</c> via the existing
/// single-shot pipeline.
/// </summary>
/// <remarks>
/// Strategy:
/// <list type="bullet">
///   <item><description>System prompt: <c>prompt.Text</c> (rendered template) plus a textual transcript of all but the last <see cref="User"/> message.</description></item>
///   <item><description>User message: text of the last <see cref="User"/> in the chat; empty string if none.</description></item>
///   <item><description>Files: last user's files take precedence; otherwise the prompt's own files are forwarded.</description></item>
/// </list>
/// Tool messages are rendered as <c>[tool {name}]: {json}</c> in the transcript;
/// fallback does NOT replay tool calls back to the model — for that, use a
/// provider with native chat support and an <c>IAgentLlm</c>.
/// </remarks>
public static class ChatFallback
{
    /// <summary>
    /// Builds a synthetic single-shot prompt from a system prompt and chat history.
    /// </summary>
    public static IPrompt<TResponse> GlueToPrompt<TResponse>(
        IPrompt<TResponse> systemPrompt,
        IReadOnlyList<ChatMessage> chat)
    {
        ArgumentNullException.ThrowIfNull(systemPrompt);
        ArgumentNullException.ThrowIfNull(chat);

        // Locate the last User message — that becomes the synthetic user turn.
        int lastUserIdx = -1;
        for (var i = chat.Count - 1; i >= 0; i--)
        {
            if (chat[i] is User)
            {
                lastUserIdx = i;
                break;
            }
        }

        // Build a textual transcript of everything except the last user message.
        var transcript = new StringBuilder();
        for (var i = 0; i < chat.Count; i++)
        {
            if (i == lastUserIdx) continue;
            switch (chat[i])
            {
                case User u:
                    transcript.Append("[user]: ").AppendLine(u.Text);
                    break;
                case Assistant a:
                    transcript.Append("[assistant]: ").AppendLine(a.Text);
                    break;
                case Tool t:
                    transcript.Append("[tool ").Append(t.Name).Append("]: ").AppendLine(t.ResultJson);
                    break;
            }
        }

        var systemText = systemPrompt.Text;
        if (transcript.Length > 0)
        {
            systemText = string.IsNullOrEmpty(systemText)
                ? "Conversation history so far:\n" + transcript
                : systemText + "\n\nConversation history so far:\n" + transcript;
        }

        var lastUser = lastUserIdx >= 0 ? (User)chat[lastUserIdx] : null;
        var userText = lastUser?.Text ?? string.Empty;

        // Per the design decision: Files live on both the system Prompt and on User.
        // Per-message files (last user) take precedence; fall back to prompt-level files.
        var files = lastUser?.Files ?? systemPrompt.Files;

        return new SyntheticPrompt<TResponse>(systemText, userText, files);
    }

    private sealed class SyntheticPrompt<TResponse> : IPrompt<TResponse>
    {
        public SyntheticPrompt(string? system, string text, IReadOnlyList<Asset>? files)
        {
            System = system;
            Text = text;
            Files = files;
        }

        public string? System { get; }
        public string Text { get; }
        public IReadOnlyList<Asset>? Files { get; }
    }

    /// <summary>
    /// Adapts a non-generic <see cref="IPrompt"/> to <see cref="IPrompt{TResponse}"/>
    /// so the streaming fallback can route through the existing single-shot
    /// stream API (which is generic).
    /// </summary>
    public sealed class PromptShim : IPrompt<string>
    {
        private readonly IPrompt _inner;
        public PromptShim(IPrompt inner) => _inner = inner;
        public string? System => _inner.System;
        public string Text => _inner.Text;
        public IReadOnlyList<Asset>? Files => _inner.Files;
    }
}
