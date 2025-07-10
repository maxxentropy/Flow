using FluentAssertions;
using McpServer.Domain.Protocol.JsonRpc;
using Xunit;

namespace McpServer.Domain.Tests.Protocol.JsonRpc;

public class JsonRpcRequestTests
{
    [Fact]
    public void JsonRpcRequest_Should_Have_Default_Version()
    {
        // Arrange & Act
        var request = new JsonRpcRequest
        {
            Jsonrpc = "2.0",
            Method = "test",
            Id = 1
        };

        // Assert
        request.Jsonrpc.Should().Be("2.0");
    }

    [Fact]
    public void JsonRpcRequest_Should_Accept_String_Id()
    {
        // Arrange & Act
        var request = new JsonRpcRequest
        {
            Jsonrpc = "2.0",
            Method = "test",
            Id = "test-id"
        };

        // Assert
        request.Id.Should().Be("test-id");
    }

    [Fact]
    public void JsonRpcRequest_Should_Accept_Null_Id_For_Notifications()
    {
        // Arrange & Act
        var request = new JsonRpcRequest
        {
            Jsonrpc = "2.0",
            Method = "test",
            Id = null
        };

        // Assert
        request.Id.Should().BeNull();
    }

    [Fact]
    public void JsonRpcRequest_Should_Accept_Params()
    {
        // Arrange
        var parameters = new { name = "test", value = 123 };

        // Act
        var request = new JsonRpcRequest
        {
            Jsonrpc = "2.0",
            Method = "test",
            Params = parameters,
            Id = 1
        };

        // Assert
        request.Params.Should().BeEquivalentTo(parameters);
    }
}