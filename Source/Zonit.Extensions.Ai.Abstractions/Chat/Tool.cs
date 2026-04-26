namespace Zonit.Extensions.Ai;

/// <summary>
/// A tool/function-call result that was executed during an agent run and
/// recorded back into the chat transcript so the model can reason over it
/// in subsequent turns.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not constructible by application code.</b> The runtime (agent runner /
/// provider adapters) creates <see cref="Tool"/> messages when tools or MCP
/// servers respond. Developers receive them only as elements of a chat list
/// returned by the runtime — for inspection, logging, or to round-trip the
/// transcript back into a follow-up <c>ChatAsync</c> call.
/// </para>
/// </remarks>
public sealed record Tool : ChatMessage
{
    /// <summary>Provider-assigned identifier of the originating tool call.</summary>
    public string ToolCallId { get; }

    /// <summary>Name of the tool that produced this result.</summary>
    public string Name { get; }

    /// <summary>Serialized result payload returned by the tool, as JSON text.</summary>
    public string ResultJson { get; }

    /// <summary>True when the tool invocation faulted; <see cref="ResultJson"/> then carries the error envelope.</summary>
    public bool IsError { get; }

    internal Tool(string toolCallId, string name, string resultJson, bool isError = false)
    {
        ToolCallId = toolCallId;
        Name = name;
        ResultJson = resultJson;
        IsError = isError;
    }
}
