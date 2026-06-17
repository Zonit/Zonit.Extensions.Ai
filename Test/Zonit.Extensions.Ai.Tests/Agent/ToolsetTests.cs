using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Zonit.Extensions.Ai.Tests.Agent;

/// <summary>
/// Tests for the <see cref="Toolset"/> declaration helpers — the fixed-arity <c>Of&lt;…&gt;()</c>
/// overloads and the unbounded fluent <c>Add&lt;T&gt;()</c> chain (<see cref="ToolsetBuilder"/>).
/// </summary>
public class ToolsetTests
{
    [Fact]
    public void Add_SingleTool_YieldsThatTool()
    {
        IReadOnlyList<Type> tools = Toolset.Add<ToolA>();

        tools.Should().Equal(typeof(ToolA));
    }

    [Fact]
    public void Add_Chain_PreservesOrderAndCount()
    {
        // The whole point: chain past the six-arg Of<…>() ceiling.
        IReadOnlyList<Type> tools = Toolset
            .Add<ToolA>().Add<ToolB>().Add<ToolC>().Add<ToolD>()
            .Add<ToolA>().Add<ToolB>().Add<ToolC>();

        tools.Should().Equal(
            typeof(ToolA), typeof(ToolB), typeof(ToolC), typeof(ToolD),
            typeof(ToolA), typeof(ToolB), typeof(ToolC));
    }

    [Fact]
    public void Add_IsImmutable_BranchingDoesNotMutateSharedPrefix()
    {
        var prefix = Toolset.Add<ToolA>().Add<ToolB>();

        IReadOnlyList<Type> branch1 = prefix.Add<ToolC>();
        IReadOnlyList<Type> branch2 = prefix.Add<ToolD>();

        ((IReadOnlyList<Type>)prefix).Should().Equal(typeof(ToolA), typeof(ToolB));
        branch1.Should().Equal(typeof(ToolA), typeof(ToolB), typeof(ToolC));
        branch2.Should().Equal(typeof(ToolA), typeof(ToolB), typeof(ToolD));
    }

    [Fact]
    public void Add_ProducesSameTypesAsEquivalentOf()
    {
        IReadOnlyList<Type> chained = Toolset.Add<ToolA>().Add<ToolB>().Add<ToolC>();
        IReadOnlyList<Type> fixedArity = Toolset.Of<ToolA, ToolB, ToolC>();

        chained.Should().Equal(fixedArity);
    }

    #region Dummy tools

    private abstract class DummyTool : ITool
    {
        public string Name => GetType().Name;
        public string Description => "dummy";
        public JsonElement InputSchema => default;
        public Task<JsonElement> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken)
            => Task.FromResult<JsonElement>(default);
    }

    private sealed class ToolA : DummyTool;
    private sealed class ToolB : DummyTool;
    private sealed class ToolC : DummyTool;
    private sealed class ToolD : DummyTool;

    #endregion
}
