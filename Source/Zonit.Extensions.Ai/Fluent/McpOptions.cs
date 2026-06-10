namespace Zonit.Extensions.Ai;

/// <summary>Mutable accumulator behind the <see cref="IMcpOptions"/> configure callback.</summary>
internal sealed class McpOptions : IMcpOptions
{
    public string[]? Allowed { get; private set; }

    public IMcpOptions AllowOnly(params string[] toolNames)
    {
        Allowed = toolNames;
        return this;
    }
}
