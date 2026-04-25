using System.ComponentModel;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Zonit.Extensions.Ai.Tests.Agent;

/// <summary>
/// Tests for <see cref="ToolBase{TInput, TOutput}"/> — schema generation,
/// argument deserialization, result serialization and exception handling.
/// </summary>
public class ToolBaseTests
{
    private sealed class EchoTool : ToolBase<EchoTool.Input, EchoTool.Output>
    {
        public override string Name => "echo";
        public override string Description => "Echoes the input message back.";

        public override Task<Output> ExecuteAsync(Input input, CancellationToken cancellationToken)
            => Task.FromResult(new Output { Echo = input.Message });

        public class Input
        {
            [Description("Message to echo.")]
            public required string Message { get; init; }
        }

        public class Output
        {
            public string Echo { get; set; } = "";
        }
    }

    private sealed class FailingTool : ToolBase<FailingTool.Input, FailingTool.Output>
    {
        public override string Name => "failing";
        public override string Description => "Always throws.";

        public override Task<Output> ExecuteAsync(Input input, CancellationToken cancellationToken)
            => throw new InvalidOperationException("boom");

        public class Input { public string? Ignored { get; init; } }
        public class Output { public bool Ok { get; set; } }
    }

    [Fact]
    public void InputSchema_ShouldIncludePropertyFromTInput()
    {
        var tool = new EchoTool();

        tool.InputSchema.ValueKind.Should().Be(JsonValueKind.Object);
        tool.InputSchema.TryGetProperty("properties", out var props).Should().BeTrue();
        props.TryGetProperty("message", out var message).Should().BeTrue();
        message.GetProperty("description").GetString().Should().Be("Message to echo.");
    }

    [Fact]
    public async Task InvokeAsync_ShouldDeserializeInputAndSerializeOutput()
    {
        ITool tool = new EchoTool();

        var args = JsonDocument.Parse("""{ "message": "hello" }""").RootElement;
        var result = await tool.InvokeAsync(args, CancellationToken.None);

        result.ValueKind.Should().Be(JsonValueKind.Object);
        result.GetProperty("echo").GetString().Should().Be("hello");
    }

    [Fact]
    public async Task InvokeAsync_ShouldPropagateExceptionsFromExecuteAsync()
    {
        ITool tool = new FailingTool();

        var args = JsonDocument.Parse("""{ }""").RootElement;
        var act = async () => await tool.InvokeAsync(args, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("boom");
    }

    [Fact]
    public void Name_ShouldMatchOverride()
    {
        ITool tool = new EchoTool();
        tool.Name.Should().Be("echo");
        tool.Description.Should().Be("Echoes the input message back.");
    }
}
