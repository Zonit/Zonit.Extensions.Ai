using FluentAssertions;
using System.ComponentModel;
using System.Text.Json;
using Xunit;

namespace Zonit.Extensions.Ai.Tests.Schema;

/// <summary>
/// Tests for JsonSchemaGenerator - schema generation for structured outputs.
/// </summary>
public class JsonSchemaGeneratorTests
{
    [Fact]
    public void Generate_SimpleObject_ShouldGenerateCorrectSchema()
    {
        // Act
        var schema = JsonSchemaGenerator.Generate<SimpleResponse>();

        // Assert
        schema.GetProperty("type").GetString().Should().Be("object");
        schema.GetProperty("properties").GetProperty("name").GetProperty("type").GetString().Should().Be("string");
        schema.GetProperty("properties").GetProperty("age").GetProperty("type").GetString().Should().Be("integer");
        schema.GetProperty("properties").GetProperty("isActive").GetProperty("type").GetString().Should().Be("boolean");
    }

    [Fact]
    public void Generate_ShouldIncludeAllPropertiesInRequired()
    {
        // Act
        var schema = JsonSchemaGenerator.Generate<SimpleResponse>();

        // Assert - OpenAI strict mode requires ALL fields in 'required'
        var required = schema.GetProperty("required").EnumerateArray().Select(x => x.GetString()).ToList();
        required.Should().Contain("name");
        required.Should().Contain("age");
        required.Should().Contain("isActive");
        required.Should().HaveCount(3);
    }

    [Fact]
    public void Generate_ShouldSetAdditionalPropertiesToFalse()
    {
        // Act
        var schema = JsonSchemaGenerator.Generate<SimpleResponse>();

        // Assert - OpenAI strict mode requires additionalProperties: false
        schema.GetProperty("additionalProperties").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public void Generate_WithDescription_ShouldIncludeDescription()
    {
        // Act
        var schema = JsonSchemaGenerator.Generate<DescribedResponse>();

        // Assert
        schema.GetProperty("description").GetString().Should().Be("A response with a description");
        schema.GetProperty("properties").GetProperty("value").GetProperty("description").GetString().Should().Be("The value field");
    }

    [Fact]
    public void Generate_ArrayProperty_ShouldGenerateArraySchema()
    {
        // Act
        var schema = JsonSchemaGenerator.Generate<ArrayResponse>();

        // Assert
        var itemsProperty = schema.GetProperty("properties").GetProperty("items");
        itemsProperty.GetProperty("type").GetString().Should().Be("array");
        itemsProperty.GetProperty("items").GetProperty("type").GetString().Should().Be("string");
    }

    [Fact]
    public void Generate_NestedObject_ShouldGenerateNestedSchema()
    {
        // Act
        var schema = JsonSchemaGenerator.Generate<NestedResponse>();

        // Assert
        var nestedProperty = schema.GetProperty("properties").GetProperty("nested");
        nestedProperty.GetProperty("type").GetString().Should().Be("object");
        nestedProperty.GetProperty("properties").GetProperty("innerValue").GetProperty("type").GetString().Should().Be("string");
    }

    [Fact]
    public void Generate_Enum_ShouldGenerateEnumSchema()
    {
        // Act
        var schema = JsonSchemaGenerator.Generate<EnumResponse>();

        // Assert
        var statusProperty = schema.GetProperty("properties").GetProperty("status");
        statusProperty.GetProperty("type").GetString().Should().Be("string");
        var enumValues = statusProperty.GetProperty("enum").EnumerateArray().Select(x => x.GetString()).ToList();
        enumValues.Should().Contain("Pending");
        enumValues.Should().Contain("Active");
        enumValues.Should().Contain("Completed");
    }

    [Fact]
    public void Generate_NumberTypes_ShouldGenerateCorrectTypes()
    {
        // Act
        var schema = JsonSchemaGenerator.Generate<NumberResponse>();

        // Assert
        schema.GetProperty("properties").GetProperty("intValue").GetProperty("type").GetString().Should().Be("integer");
        schema.GetProperty("properties").GetProperty("doubleValue").GetProperty("type").GetString().Should().Be("number");
        schema.GetProperty("properties").GetProperty("decimalValue").GetProperty("type").GetString().Should().Be("number");
    }

    [Fact]
    public void GetDescription_ShouldReturnTypeDescription()
    {
        // Act
        var description = JsonSchemaGenerator.GetDescription<DescribedResponse>();

        // Assert
        description.Should().Be("A response with a description");
    }

    [Fact]
    public void GetDescription_WithoutAttribute_ShouldReturnNull()
    {
        // Act
        var description = JsonSchemaGenerator.GetDescription<SimpleResponse>();

        // Assert
        description.Should().BeNull();
    }

    // Test models
    private class SimpleResponse
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
        public bool IsActive { get; set; }
    }

    [Description("A response with a description")]
    private class DescribedResponse
    {
        [Description("The value field")]
        public string Value { get; set; } = "";
    }

    private class ArrayResponse
    {
        public string[] Items { get; set; } = [];
    }

    private class NestedResponse
    {
        public InnerObject Nested { get; set; } = new();
    }

    private class InnerObject
    {
        public string InnerValue { get; set; } = "";
    }

    private class EnumResponse
    {
        public StatusEnum Status { get; set; }
    }

    private enum StatusEnum
    {
        Pending,
        Active,
        Completed
    }

    private class NumberResponse
    {
        public int IntValue { get; set; }
        public double DoubleValue { get; set; }
        public decimal DecimalValue { get; set; }
    }
}
