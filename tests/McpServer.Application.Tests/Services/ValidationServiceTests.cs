using System.Text.Json;
using FluentAssertions;
using McpServer.Application.Services;
using McpServer.Domain.Tools;
using McpServer.Domain.Validation;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace McpServer.Application.Tests.Services;

/// <summary>
/// Tests for the FluentValidation-based validation service.
/// </summary>
public class ValidationServiceTests
{
    private readonly ValidationService _validationService;
    private readonly Mock<ILogger<ValidationService>> _logger;

    public ValidationServiceTests()
    {
        _logger = new Mock<ILogger<ValidationService>>();
        _validationService = new ValidationService(_logger.Object);
    }

    [Fact]
    public void ValidateJsonRpcRequest_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var json = """
        {
            "jsonrpc": "2.0",
            "method": "test",
            "params": {"key": "value"},
            "id": 1
        }
        """;
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = _validationService.ValidateJsonRpcRequest(element);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateJsonRpcRequest_MissingJsonRpc_ReturnsError()
    {
        // Arrange
        var json = """
        {
            "method": "test",
            "id": 1
        }
        """;
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = _validationService.ValidateJsonRpcRequest(element);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorCode == "invalid_jsonrpc");
    }

    [Fact]
    public void ValidateJsonRpcRequest_InvalidJsonRpcVersion_ReturnsError()
    {
        // Arrange
        var json = """
        {
            "jsonrpc": "1.0",
            "method": "test",
            "id": 1
        }
        """;
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = _validationService.ValidateJsonRpcRequest(element);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorCode == "invalid_jsonrpc");
    }

    [Fact]
    public void ValidateJsonRpcRequest_MissingMethod_ReturnsError()
    {
        // Arrange
        var json = """
        {
            "jsonrpc": "2.0",
            "id": 1
        }
        """;
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = _validationService.ValidateJsonRpcRequest(element);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorCode == "missing_method");
    }

    [Fact]
    public void ValidateJsonRpcRequest_EmptyMethod_ReturnsError()
    {
        // Arrange
        var json = """
        {
            "jsonrpc": "2.0",
            "method": "",
            "id": 1
        }
        """;
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = _validationService.ValidateJsonRpcRequest(element);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorCode == "missing_method");
    }

    [Fact]
    public void ValidateJsonRpcRequest_InvalidParamsType_ReturnsError()
    {
        // Arrange
        var json = """
        {
            "jsonrpc": "2.0",
            "method": "test",
            "params": "invalid",
            "id": 1
        }
        """;
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = _validationService.ValidateJsonRpcRequest(element);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorCode == "invalid_params");
    }

    [Fact]
    public void ValidateJsonRpcRequest_ValidNotification_ReturnsSuccess()
    {
        // Arrange
        var json = """
        {
            "jsonrpc": "2.0",
            "method": "notification"
        }
        """;
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = _validationService.ValidateJsonRpcRequest(element);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateJsonRpcRequest_ExtraProperties_ReturnsError()
    {
        // Arrange
        var json = """
        {
            "jsonrpc": "2.0",
            "method": "test",
            "id": 1,
            "extra": "property"
        }
        """;
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = _validationService.ValidateJsonRpcRequest(element);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorCode == "unexpected_properties");
    }

    [Fact]
    public void ValidateJsonRpcRequest_ValidMetaField_ReturnsSuccess()
    {
        // Arrange
        var json = """
        {
            "jsonrpc": "2.0",
            "method": "test",
            "id": 1,
            "_meta": {
                "progressToken": "token123"
            }
        }
        """;
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = _validationService.ValidateJsonRpcRequest(element);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateMcpMessage_InitializeRequest_Valid_ReturnsSuccess()
    {
        // Arrange
        var json = """
        {
            "jsonrpc": "2.0",
            "method": "initialize",
            "params": {
                "protocolVersion": "1.0.0",
                "capabilities": {},
                "clientInfo": {
                    "name": "TestClient",
                    "version": "1.0.0"
                }
            },
            "id": 1
        }
        """;
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = _validationService.ValidateMcpMessage(element, "initialize");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateMcpMessage_InitializeRequest_InvalidVersion_ReturnsError()
    {
        // Arrange
        var json = """
        {
            "jsonrpc": "2.0",
            "method": "initialize",
            "params": {
                "protocolVersion": "invalid",
                "capabilities": {},
                "clientInfo": {
                    "name": "TestClient",
                    "version": "1.0.0"
                }
            },
            "id": 1
        }
        """;
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = _validationService.ValidateMcpMessage(element, "initialize");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorCode == "invalid_protocol_version");
    }

    [Fact]
    public void ValidateMcpMessage_ToolsCallRequest_Valid_ReturnsSuccess()
    {
        // Arrange
        var json = """
        {
            "jsonrpc": "2.0",
            "method": "tools/call",
            "params": {
                "name": "testTool",
                "arguments": {
                    "arg1": "value1"
                }
            },
            "id": 1
        }
        """;
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = _validationService.ValidateMcpMessage(element, "tools/call");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateMcpMessage_ToolsCallRequest_MissingName_ReturnsError()
    {
        // Arrange
        var json = """
        {
            "jsonrpc": "2.0",
            "method": "tools/call",
            "params": {
                "arguments": {}
            },
            "id": 1
        }
        """;
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = _validationService.ValidateMcpMessage(element, "tools/call");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorCode == "invalid_tool_name");
    }

    [Fact]
    public void ValidateToolArguments_ValidArguments_ReturnsSuccess()
    {
        // Arrange
        var schema = new ToolSchema
        {
            Type = "object",
            Properties = new Dictionary<string, object>
            {
                ["name"] = JsonDocument.Parse("""{"type": "string"}""").RootElement,
                ["age"] = JsonDocument.Parse("""{"type": "number"}""").RootElement
            },
            Required = new List<string> { "name" },
            AdditionalProperties = false
        };

        var arguments = new Dictionary<string, object?>
        {
            ["name"] = "John",
            ["age"] = 30
        };

        // Act
        var result = _validationService.ValidateToolArguments(arguments, schema);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateToolArguments_MissingRequired_ReturnsError()
    {
        // Arrange
        var schema = new ToolSchema
        {
            Type = "object",
            Properties = new Dictionary<string, object>
            {
                ["name"] = JsonDocument.Parse("""{"type": "string"}""").RootElement
            },
            Required = new List<string> { "name" },
            AdditionalProperties = false
        };

        var arguments = new Dictionary<string, object?>();

        // Act
        var result = _validationService.ValidateToolArguments(arguments, schema);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorCode == "missing_required_property");
    }

    [Fact]
    public void ValidateToolArguments_ExtraProperty_WhenNotAllowed_ReturnsError()
    {
        // Arrange
        var schema = new ToolSchema
        {
            Type = "object",
            Properties = new Dictionary<string, object>
            {
                ["name"] = JsonDocument.Parse("""{"type": "string"}""").RootElement
            },
            AdditionalProperties = false
        };

        var arguments = new Dictionary<string, object?>
        {
            ["name"] = "John",
            ["extra"] = "value"
        };

        // Act
        var result = _validationService.ValidateToolArguments(arguments, schema);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorCode == "unexpected_properties");
    }

    [Fact]
    public void ValidateToolArguments_InvalidType_ReturnsError()
    {
        // Arrange
        var schema = new ToolSchema
        {
            Type = "object",
            Properties = new Dictionary<string, object>
            {
                ["age"] = JsonDocument.Parse("""{"type": "number"}""").RootElement
            }
        };

        var arguments = new Dictionary<string, object?>
        {
            ["age"] = "not a number"
        };

        // Act
        var result = _validationService.ValidateToolArguments(arguments, schema);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorCode == "invalid_property_type");
    }

    [Fact]
    public void ValidateMcpMessage_UnknownMessageType_FallsBackToJsonRpc()
    {
        // Arrange
        var json = """
        {
            "jsonrpc": "2.0",
            "method": "unknown/method",
            "id": 1
        }
        """;
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = _validationService.ValidateMcpMessage(element, "unknown/method");

        // Assert
        // Should still validate as valid JSON-RPC
        result.IsValid.Should().BeTrue();
        result.Context.Should().ContainKey("messageType");
        result.Context!["messageType"].Should().Be("unknown/method");
    }
}