using FluentAssertions;
using McpServer.Domain.Tools;
using System.Collections.Generic;
using Xunit;

namespace McpServer.Domain.Tests.Tools;

public class ToolSchemaTests
{
    private static readonly string[] ExpectedRequiredFields = new[] { "name", "age" };
    [Fact]
    public void ToolSchema_Should_Be_Created_With_Type()
    {
        // Arrange & Act
        var schema = new ToolSchema
        {
            Type = "object"
        };

        // Assert
        schema.Type.Should().Be("object");
        schema.Properties.Should().BeNull();
        schema.Required.Should().BeNull();
        schema.AdditionalProperties.Should().BeNull();
    }

    [Fact]
    public void ToolSchema_Should_Accept_Properties()
    {
        // Arrange
        var properties = new Dictionary<string, object>
        {
            ["name"] = new { type = "string", description = "The name" },
            ["age"] = new { type = "number", description = "The age" }
        };

        // Act
        var schema = new ToolSchema
        {
            Type = "object",
            Properties = properties
        };

        // Assert
        schema.Properties.Should().BeEquivalentTo(properties);
    }

    [Fact]
    public void ToolSchema_Should_Accept_Required_Fields()
    {
        // Arrange & Act
        var schema = new ToolSchema
        {
            Type = "object",
            Required = new List<string> { "name", "age" }
        };

        // Assert
        schema.Required.Should().BeEquivalentTo(ExpectedRequiredFields);
    }

    [Fact]
    public void ToolSchema_Should_Accept_AdditionalProperties_Setting()
    {
        // Arrange & Act
        var schema = new ToolSchema
        {
            Type = "object",
            AdditionalProperties = false
        };

        // Assert
        schema.AdditionalProperties.Should().BeFalse();
    }
}