using FluentAssertions;
using McpServer.Application.Server;
using McpServer.Application.Services;
using McpServer.Domain.Protocol.JsonRpc;
using McpServer.Domain.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace McpServer.Application.Tests.Server;

public class MessageRouterTests
{
    private readonly Mock<ILogger<MessageRouter>> _loggerMock;
    private readonly Mock<IProgressTracker> _progressTrackerMock;
    private readonly Mock<IErrorResponseBuilder> _errorResponseBuilderMock;
    private readonly List<IMessageHandler> _handlers;
    private readonly MessageRouter _router;

    public MessageRouterTests()
    {
        _loggerMock = new Mock<ILogger<MessageRouter>>();
        _progressTrackerMock = new Mock<IProgressTracker>();
        _errorResponseBuilderMock = new Mock<IErrorResponseBuilder>();
        _handlers = new List<IMessageHandler>();
        
        // Set up the error response builder mock to return proper responses
        _errorResponseBuilderMock.Setup(x => x.CreateErrorResponse(It.IsAny<object?>(), It.IsAny<Exception>()))
            .Returns<object?, Exception>((id, ex) => new JsonRpcResponse
            {
                Jsonrpc = "2.0",
                Error = new JsonRpcError
                {
                    Code = JsonRpcErrorCodes.ParseError,
                    Message = ex.Message
                },
                Id = id
            });
            
        _errorResponseBuilderMock.Setup(x => x.CreateErrorResponse(It.IsAny<object?>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<object?>()))
            .Returns<object?, int, string, object?>((id, code, message, data) => new JsonRpcResponse
            {
                Jsonrpc = "2.0",
                Error = new JsonRpcError
                {
                    Code = code,
                    Message = message,
                    Data = data
                },
                Id = id
            });
        
        _router = new MessageRouter(_loggerMock.Object, _handlers, _progressTrackerMock.Object, _errorResponseBuilderMock.Object, null);
    }

    [Fact]
    public async Task RouteMessageAsync_Should_Return_ParseError_For_Invalid_Json()
    {
        // Arrange
        var invalidJson = "{ invalid json";

        // Act
        var result = await _router.RouteMessageAsync(invalidJson);

        // Assert
        result.Should().BeOfType<JsonRpcResponse>();
        var response = result as JsonRpcResponse;
        response!.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(McpErrorCodes.ParseError);
    }

    [Fact]
    public async Task RouteMessageAsync_Should_Return_InvalidRequest_For_Missing_Version()
    {
        // Arrange
        var message = JsonSerializer.Serialize(new { method = "test", id = 1 });

        // Act
        var result = await _router.RouteMessageAsync(message);

        // Assert
        result.Should().BeOfType<JsonRpcResponse>();
        var response = result as JsonRpcResponse;
        response!.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(McpErrorCodes.InvalidRequest);
        response.Error.Message.Should().Contain("Invalid JSON-RPC version");
    }

    [Fact]
    public async Task RouteMessageAsync_Should_Return_InvalidRequest_For_Missing_Method()
    {
        // Arrange
        var message = JsonSerializer.Serialize(new { jsonrpc = "2.0", id = 1 });

        // Act
        var result = await _router.RouteMessageAsync(message);

        // Assert
        result.Should().BeOfType<JsonRpcResponse>();
        var response = result as JsonRpcResponse;
        response!.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(McpErrorCodes.InvalidRequest);
        response.Error.Message.Should().Contain("Missing method");
    }

    [Fact]
    public async Task RouteMessageAsync_Should_Return_MethodNotFound_For_Unknown_Method()
    {
        // Arrange
        var message = JsonSerializer.Serialize(new 
        { 
            jsonrpc = "2.0", 
            method = "unknown_method", 
            id = 1 
        });

        // Act
        var result = await _router.RouteMessageAsync(message);

        // Assert
        result.Should().BeOfType<JsonRpcResponse>();
        var response = result as JsonRpcResponse;
        response!.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(McpErrorCodes.MethodNotFound);
        response.Error.Message.Should().Contain("unknown_method");
    }

    [Fact]
    public async Task RouteMessageAsync_Should_Return_Null_For_Notification_With_Unknown_Method()
    {
        // Arrange
        var message = JsonSerializer.Serialize(new 
        { 
            jsonrpc = "2.0", 
            method = "unknown_method"
            // No id = notification
        });

        // Act
        var result = await _router.RouteMessageAsync(message);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RouteMessageAsync_Should_Handle_String_Id()
    {
        // Arrange
        var message = JsonSerializer.Serialize(new 
        { 
            jsonrpc = "2.0", 
            method = "unknown_method",
            id = "string-id"
        });

        // Act
        var result = await _router.RouteMessageAsync(message);

        // Assert
        result.Should().BeOfType<JsonRpcResponse>();
        var response = result as JsonRpcResponse;
        response!.Id.Should().Be("string-id");
    }

    [Fact]
    public async Task RouteMessageAsync_Should_Handle_Number_Id()
    {
        // Arrange
        var message = JsonSerializer.Serialize(new 
        { 
            jsonrpc = "2.0", 
            method = "unknown_method",
            id = 123
        });

        // Act
        var result = await _router.RouteMessageAsync(message);

        // Assert
        result.Should().BeOfType<JsonRpcResponse>();
        var response = result as JsonRpcResponse;
        response!.Id.Should().Be(123L); // JSON deserializes to long
    }
}