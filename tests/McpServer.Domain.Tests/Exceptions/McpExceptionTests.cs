using FluentAssertions;
using McpServer.Domain.Exceptions;
using System;
using Xunit;

namespace McpServer.Domain.Tests.Exceptions;

public class McpExceptionTests
{
    [Fact]
    public void McpException_Should_Be_Created_With_Message()
    {
        // Arrange & Act
        var exception = new McpException("Test error");

        // Assert
        exception.Message.Should().Be("Test error");
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void ProtocolException_Should_Be_McpException()
    {
        // Arrange & Act
        var exception = new ProtocolException("Protocol error");

        // Assert
        exception.Should().BeAssignableTo<McpException>();
        exception.Message.Should().Be("Protocol error");
    }

    [Fact]
    public void ToolExecutionException_Should_Include_ToolName_In_Message()
    {
        // Arrange & Act
        var exception = new ToolExecutionException("myTool", "Failed to execute");

        // Assert
        exception.Should().BeAssignableTo<McpException>();
        exception.Message.Should().Contain("myTool");
        exception.Message.Should().Contain("Failed to execute");
        exception.ToolName.Should().Be("myTool");
    }

    [Fact]
    public void ResourceException_Should_Include_Uri_In_Message()
    {
        // Arrange & Act
        var exception = new ResourceException("file://test.txt", "File not found");

        // Assert
        exception.Should().BeAssignableTo<McpException>();
        exception.Message.Should().Contain("file://test.txt");
        exception.Message.Should().Contain("File not found");
        exception.Uri.Should().Be("file://test.txt");
    }

    [Fact]
    public void TransportException_Should_Support_InnerException()
    {
        // Arrange
        var innerException = new InvalidOperationException("Connection failed");

        // Act
        var exception = new TransportException("Transport error", innerException);

        // Assert
        exception.Should().BeAssignableTo<McpException>();
        exception.Message.Should().Be("Transport error");
        exception.InnerException.Should().Be(innerException);
    }
}