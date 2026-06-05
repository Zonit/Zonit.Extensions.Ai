namespace Zonit.Extensions.Ai;

/// <summary>
/// Policy for handling exceptions thrown from a tool's execution during the agent loop.
/// </summary>
public enum ToolExceptionPolicy
{
    /// <summary>
    /// Default. Catch the exception, serialize it to a JSON error object and
    /// return it to the model as the tool result. The model can then retry,
    /// fall back to another tool, or explain the failure to the user.
    /// </summary>
    ReturnErrorToModel = 0,

    /// <summary>
    /// Propagate the exception out of <c>GenerateAsync</c> unchanged,
    /// terminating the agent loop immediately.
    /// </summary>
    ThrowToCaller = 1,
}
