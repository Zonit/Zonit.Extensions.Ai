using FluentAssertions;
using Xunit;

namespace Zonit.Extensions.Ai.Tests.Tools;

/// <summary>
/// Tests for Tool models - WebSearchTool, FileSearchTool, FunctionTool, CodeInterpreterTool.
/// </summary>
public class ToolTests
{
    [Fact]
    public void WebSearchTool_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var tool = new WebSearchTool();

        // Assert
        tool.Country.Should().BeNull();
        tool.Region.Should().BeNull();
        tool.City.Should().BeNull();
        tool.TimeZone.Should().BeNull();
        tool.ContextSize.Should().Be(WebSearchTool.ContextSizeType.Medium);
    }

    [Fact]
    public void WebSearchTool_ShouldAcceptAllContextSizes()
    {
        // Arrange & Act
        var lowTool = new WebSearchTool { ContextSize = WebSearchTool.ContextSizeType.Low };
        var mediumTool = new WebSearchTool { ContextSize = WebSearchTool.ContextSizeType.Medium };
        var highTool = new WebSearchTool { ContextSize = WebSearchTool.ContextSizeType.High };

        // Assert
        lowTool.ContextSize.Should().Be(WebSearchTool.ContextSizeType.Low);
        mediumTool.ContextSize.Should().Be(WebSearchTool.ContextSizeType.Medium);
        highTool.ContextSize.Should().Be(WebSearchTool.ContextSizeType.High);
    }

    [Fact]
    public void FileSearchTool_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var tool = new FileSearchTool();

        // Assert
        tool.VectorId.Should().BeNull();
        tool.MaxNumResults.Should().BeNull();
        tool.RankingOptions.Should().BeNull();
        tool.Filters.Should().BeNull();
    }

    [Fact]
    public void FileSearchTool_ShouldAcceptVectorStoreConfig()
    {
        // Arrange & Act
        var tool = new FileSearchTool
        {
            VectorId = "vs_12345",
            MaxNumResults = 10,
            RankingOptions = new FileSearchTool.RankingOptionsType
            {
                Ranker = "auto",
                ScoreThreshold = 0.5
            },
            Filters = new { type = "eq", key = "category", value = "docs" }
        };

        // Assert
        tool.VectorId.Should().Be("vs_12345");
        tool.MaxNumResults.Should().Be(10);
        tool.RankingOptions.Should().NotBeNull();
        tool.RankingOptions!.Ranker.Should().Be("auto");
        tool.RankingOptions!.ScoreThreshold.Should().Be(0.5);
        tool.Filters.Should().NotBeNull();
    }

    [Fact]
    public void FunctionTool_ShouldHaveRequiredProperties()
    {
        // Arrange & Act
        var tool = FunctionTool.Create(
            "get_weather",
            "Gets weather for a location",
            new { type = "object", properties = new { location = new { type = "string" } } }
        );

        // Assert
        tool.Name.Should().Be("get_weather");
        tool.Description.Should().Be("Gets weather for a location");
        tool.Parameters.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
    }

    [Fact]
    public void CodeInterpreterTool_ShouldBeInstantiable()
    {
        // Arrange & Act
        var tool = new CodeInterpreterTool();

        // Assert
        tool.Should().NotBeNull();
    }
}
