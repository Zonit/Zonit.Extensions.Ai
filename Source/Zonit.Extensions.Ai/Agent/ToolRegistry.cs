using System.Collections.Concurrent;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Default <see cref="IToolRegistry"/> implementation backed by the
/// <see cref="ITool"/> instances registered in the DI container.
/// </summary>
/// <remarks>
/// Duplicate <see cref="ITool.Name"/> values across registered tools throw
/// at resolution time — each agent-visible name must be unique.
/// </remarks>
internal sealed class ToolRegistry : IToolRegistry
{
    private readonly IReadOnlyList<ITool> _tools;
    private readonly ConcurrentDictionary<string, ITool> _byName;

    public ToolRegistry(IEnumerable<ITool> tools)
    {
        _tools = tools.ToArray();
        _byName = new ConcurrentDictionary<string, ITool>(StringComparer.Ordinal);

        foreach (var tool in _tools)
        {
            if (string.IsNullOrWhiteSpace(tool.Name))
            {
                throw new InvalidOperationException(
                    $"Tool of type {tool.GetType().FullName} has an empty Name. " +
                    "Every ITool must expose a non-empty, unique Name.");
            }

            if (!_byName.TryAdd(tool.Name, tool))
            {
                var existing = _byName[tool.Name].GetType().FullName;
                var duplicate = tool.GetType().FullName;
                throw new InvalidOperationException(
                    $"Duplicate tool name '{tool.Name}' detected: " +
                    $"'{existing}' and '{duplicate}'. Tool names must be unique.");
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ITool> GetAll() => _tools;

    /// <inheritdoc />
    public ITool? Get(string name)
        => _byName.TryGetValue(name, out var tool) ? tool : null;
}
