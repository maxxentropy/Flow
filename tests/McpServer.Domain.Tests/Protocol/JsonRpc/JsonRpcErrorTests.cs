using FluentAssertions;
using McpServer.Domain.Protocol.JsonRpc;
using Xunit;

namespace McpServer.Domain.Tests.Protocol.JsonRpc;

public class JsonRpcErrorTests
{
    [Theory]
    [InlineData(JsonRpcErrorCodes.ParseError, -32700)]
    [InlineData(JsonRpcErrorCodes.InvalidRequest, -32600)]
    [InlineData(JsonRpcErrorCodes.MethodNotFound, -32601)]
    [InlineData(JsonRpcErrorCodes.InvalidParams, -32602)]
    [InlineData(JsonRpcErrorCodes.InternalError, -32603)]
    public void JsonRpcErrorCodes_Should_Have_Correct_Values(int actualCode, int expectedCode)
    {
        // Assert
        actualCode.Should().Be(expectedCode);
    }

    [Fact]
    public void JsonRpcError_Should_Be_Created_With_Required_Properties()
    {
        // Arrange & Act
        var error = new JsonRpcError
        {
            Code = JsonRpcErrorCodes.MethodNotFound,
            Message = "Method not found"
        };

        // Assert
        error.Code.Should().Be(JsonRpcErrorCodes.MethodNotFound);
        error.Message.Should().Be("Method not found");
        error.Data.Should().BeNull();
    }

    [Fact]
    public void JsonRpcError_Should_Accept_Data_Property()
    {
        // Arrange
        var additionalData = new { details = "Additional error information" };

        // Act
        var error = new JsonRpcError
        {
            Code = JsonRpcErrorCodes.InvalidParams,
            Message = "Invalid parameters",
            Data = additionalData
        };

        // Assert
        error.Data.Should().BeEquivalentTo(additionalData);
    }
}