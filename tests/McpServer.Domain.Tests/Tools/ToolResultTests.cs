using FluentAssertions;
using McpServer.Domain.Tools;
using System.Collections.Generic;
using Xunit;

namespace McpServer.Domain.Tests.Tools;

public class ToolResultTests
{
    [Fact]
    public void ToolResult_Should_Contain_Content_List()
    {
        // Arrange
        var content = new List<ToolContent>
        {
            new TextContent { Text = "Result text" }
        };

        // Act
        var result = new ToolResult
        {
            Content = content
        };

        // Assert
        result.Content.Should().HaveCount(1);
        result.Content[0].Should().BeOfType<TextContent>();
        result.IsError.Should().BeNull();
    }

    [Fact]
    public void ToolResult_Should_Support_Error_Flag()
    {
        // Arrange & Act
        var result = new ToolResult
        {
            Content = new List<ToolContent>
            {
                new TextContent { Text = "Error occurred" }
            },
            IsError = true
        };

        // Assert
        result.IsError.Should().BeTrue();
    }

    [Fact]
    public void TextContent_Should_Have_Correct_Type()
    {
        // Arrange & Act
        var content = new TextContent { Text = "Test text" };

        // Assert
        content.Type.Should().Be("text");
        content.Text.Should().Be("Test text");
    }

    [Fact]
    public void ImageContent_Should_Have_Correct_Type()
    {
        // Arrange & Act
        var content = new ImageContent 
        { 
            Data = "base64data",
            MimeType = "image/png"
        };

        // Assert
        content.Type.Should().Be("image");
        content.Data.Should().Be("base64data");
        content.MimeType.Should().Be("image/png");
    }
}