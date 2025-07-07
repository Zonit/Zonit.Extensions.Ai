using System.ComponentModel;

namespace Zonit.Extensions.Ai.Llm.X;

public enum ModeType
{
    /// <summary>
    /// Never use web search
    /// </summary>
    [Description("never")]
    Never,

    /// <summary>
    /// Always use web search
    /// </summary>
    [Description("always")]
    Always,

    /// <summary>
    /// Automatically determine when to use web search based on the query
    /// </summary>
    [Description("auto")]
    Auto
}