using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Zonit.Extensions.Ai.Tests.Agent;

/// <summary>
/// DI registration tests for tools and MCP servers.
/// </summary>
public class AgentRegistrationTests
{
    private sealed class ToolA : ToolBase<ToolA.In, ToolA.Out>
    {
        public override string Name => "tool_a";
        public override string Description => "A";
        public override Task<Out> ExecuteAsync(In input, CancellationToken ct) => Task.FromResult(new Out());
        public class In { }
        public class Out { }
    }

    private sealed class ToolB : ToolBase<ToolB.In, ToolB.Out>
    {
        public override string Name => "tool_b";
        public override string Description => "B";
        public override Task<Out> ExecuteAsync(In input, CancellationToken ct) => Task.FromResult(new Out());
        public class In { }
        public class Out { }
    }

    private sealed class DuplicateNameTool : ToolBase<DuplicateNameTool.In, DuplicateNameTool.Out>
    {
        public override string Name => "tool_a";   // intentional duplicate of ToolA
        public override string Description => "dup";
        public override Task<Out> ExecuteAsync(In input, CancellationToken ct) => Task.FromResult(new Out());
        public class In { }
        public class Out { }
    }

    [Fact]
    public void AddAiTool_ShouldExposeToolThroughRegistry()
    {
        var services = new ServiceCollection();
        services.AddAiTool<ToolA>();

        using var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IToolRegistry>();

        registry.GetAll().Should().HaveCount(1);
        registry.Get("tool_a").Should().NotBeNull();
        registry.Get("tool_a").Should().BeOfType<ToolA>();
        registry.Get("missing").Should().BeNull();
    }

    [Fact]
    public void AddAiTool_ShouldBeIdempotent()
    {
        var services = new ServiceCollection();
        services.AddAiTool<ToolA>();
        services.AddAiTool<ToolA>();    // twice on purpose

        using var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IToolRegistry>();

        registry.GetAll().Should().HaveCount(1);
    }

    [Fact]
    public void AddAiTool_MultipleDistinctTools_AllExposed()
    {
        var services = new ServiceCollection();
        services.AddAiTool<ToolA>();
        services.AddAiTool<ToolB>();

        using var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IToolRegistry>();

        registry.GetAll().Select(t => t.Name).Should().BeEquivalentTo(new[] { "tool_a", "tool_b" });
    }

    [Fact]
    public void AddAiTool_DuplicateName_ShouldThrowOnResolution()
    {
        var services = new ServiceCollection();
        services.AddAi();
        services.AddAiTool<ToolA>();
        services.AddAiTool<DuplicateNameTool>();

        using var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var act = () => scope.ServiceProvider.GetRequiredService<IToolRegistry>();

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*Duplicate tool name 'tool_a'*");
    }

    [Fact]
    public void AddAiTool_FactoryOverload_ShouldBuildToolLazily()
    {
        var buildCount = 0;
        var services = new ServiceCollection();
        services.AddAiTool<ToolA>(sp =>
        {
            buildCount++;
            return new ToolA();
        });

        using var sp = services.BuildServiceProvider();

        using (var scope = sp.CreateScope())
        {
            var registry = scope.ServiceProvider.GetRequiredService<IToolRegistry>();
            registry.Get("tool_a").Should().NotBeNull();
        }

        buildCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void AddAiMcp_ShouldExposeServerThroughRegistry()
    {
        var mcp = new Mcp("github", "https://mcp.example.com/sse", "tok");

        var services = new ServiceCollection();
        services.AddAi();
        services.AddAiMcp(mcp);

        using var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<IMcpRegistry>();

        registry.GetAll().Should().ContainSingle().Which.Should().Be(mcp);
        registry.Get("github").Should().Be(mcp);
        registry.Get("missing").Should().BeNull();
    }

    [Fact]
    public void AddAiMcp_DuplicateName_ShouldThrowOnResolution()
    {
        var services = new ServiceCollection();
        services.AddAi();
        services.AddAiMcp(new Mcp("same", "https://a.example.com/sse"));
        services.AddAiMcp(new Mcp("same", "https://b.example.com/sse"));

        using var sp = services.BuildServiceProvider();
        var act = () => sp.GetRequiredService<IMcpRegistry>();

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*Duplicate MCP server name 'same'*");
    }

    [Fact]
    public void AddAiMcp_Null_ShouldThrow()
    {
        var services = new ServiceCollection();
        var act = () => services.AddAiMcp(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
