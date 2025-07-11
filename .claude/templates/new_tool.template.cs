using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using McpServer.Domain.Tools;
using McpServer.Domain.Models;

namespace McpServer.Infrastructure.Tools;

/// <summary>
/// {{TOOL_DESCRIPTION}}
/// </summary>
public class {{TOOL_NAME}}Tool : ITool
{
    private readonly ILogger<{{TOOL_NAME}}Tool> _logger;
    private readonly {{DEPENDENCY_TYPE}}? _dependency;

    public string Name => "{{TOOL_NAME_LOWER}}";
    
    public string Description => "{{TOOL_DESCRIPTION}}";
    
    public ToolSchema Schema => new()
    {
        Type = "object",
        Properties = new Dictionary<string, JsonElement>
        {
            ["{{PARAM_NAME}}"] = JsonSerializer.SerializeToElement(new
            {
                type = "{{PARAM_TYPE}}",
                description = "{{PARAM_DESCRIPTION}}"
            }),
            // TODO: Add more parameters as needed
        },
        Required = new[] { "{{PARAM_NAME}}" }
    };

    public {{TOOL_NAME}}Tool(
        ILogger<{{TOOL_NAME}}Tool> logger,
        {{DEPENDENCY_TYPE}}? dependency = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dependency = dependency;
    }

    /// <summary>
    /// Executes the tool with the provided parameters
    /// </summary>
    public async Task<ToolResult> ExecuteAsync(
        ToolRequest request, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Executing {ToolName} with parameters: {Parameters}", 
            Name, request.Arguments);

        try
        {
            // Parse and validate parameters
            var parameters = ParseParameters(request.Arguments);
            ValidateParameters(parameters);

            // Execute tool logic
            var result = await ExecuteInternalAsync(parameters, cancellationToken);

            _logger.LogInformation("{ToolName} executed successfully", Name);
            
            return new ToolResult
            {
                Success = true,
                Content = new[]
                {
                    new ToolResultContent
                    {
                        Type = "text",
                        Text = result
                    }
                }
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("{ToolName} execution cancelled", Name);
            throw;
        }
        catch (ToolValidationException ex)
        {
            _logger.LogWarning(ex, "{ToolName} validation failed", Name);
            return new ToolResult
            {
                Success = false,
                Error = new ToolError
                {
                    Code = "VALIDATION_ERROR",
                    Message = ex.Message
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ToolName} execution failed", Name);
            return new ToolResult
            {
                Success = false,
                Error = new ToolError
                {
                    Code = "EXECUTION_ERROR",
                    Message = $"Tool execution failed: {ex.Message}"
                }
            };
        }
    }

    private {{TOOL_NAME}}Parameters ParseParameters(JsonElement arguments)
    {
        try
        {
            var parameters = arguments.Deserialize<{{TOOL_NAME}}Parameters>(
                new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });
            
            return parameters ?? throw new ToolValidationException("Invalid parameters");
        }
        catch (JsonException ex)
        {
            throw new ToolValidationException($"Failed to parse parameters: {ex.Message}", ex);
        }
    }

    private void ValidateParameters({{TOOL_NAME}}Parameters parameters)
    {
        // TODO: Add parameter validation logic
        {{VALIDATION_LOGIC}}
    }

    private async Task<string> ExecuteInternalAsync(
        {{TOOL_NAME}}Parameters parameters,
        CancellationToken cancellationToken)
    {
        // TODO: Implement tool logic
        {{EXECUTION_LOGIC}}
        
        await Task.Delay(100, cancellationToken); // Simulate work
        
        return $"Executed {Name} successfully";
    }

    /// <summary>
    /// Parameters for the {{TOOL_NAME}} tool
    /// </summary>
    private record {{TOOL_NAME}}Parameters
    {
        public required string {{PARAM_NAME}} { get; init; }
        // TODO: Add more parameters as needed
    }
}

// Registration extension
public static class {{TOOL_NAME}}ToolExtensions
{
    public static IServiceCollection Add{{TOOL_NAME}}Tool(this IServiceCollection services)
    {
        services.AddTransient<ITool, {{TOOL_NAME}}Tool>();
        services.AddTransient<{{TOOL_NAME}}Tool>();
        return services;
    }
}

// Unit test template
#if TEST_TEMPLATE
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace McpServer.Infrastructure.Tests.Tools;

[TestFixture]
public class {{TOOL_NAME}}ToolTests
{
    private Mock<ILogger<{{TOOL_NAME}}Tool>> _loggerMock;
    private {{TOOL_NAME}}Tool _tool;

    [SetUp]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<{{TOOL_NAME}}Tool>>();
        _tool = new {{TOOL_NAME}}Tool(_loggerMock.Object);
    }

    [Test]
    public void Name_ReturnsExpectedValue()
    {
        Assert.That(_tool.Name, Is.EqualTo("{{TOOL_NAME_LOWER}}"));
    }

    [Test]
    public async Task ExecuteAsync_ValidParameters_ReturnsSuccess()
    {
        // Arrange
        var request = new ToolRequest
        {
            Name = _tool.Name,
            Arguments = JsonSerializer.SerializeToElement(new
            {
                {{PARAM_NAME}} = "test_value"
            })
        };

        // Act
        var result = await _tool.ExecuteAsync(request);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Content, Is.Not.Empty);
    }

    [Test]
    public async Task ExecuteAsync_InvalidParameters_ReturnsError()
    {
        // Arrange
        var request = new ToolRequest
        {
            Name = _tool.Name,
            Arguments = JsonSerializer.SerializeToElement(new { })
        };

        // Act
        var result = await _tool.ExecuteAsync(request);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.Not.Null);
        Assert.That(result.Error.Code, Is.EqualTo("VALIDATION_ERROR"));
    }

    [Test]
    public async Task ExecuteAsync_Cancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var request = new ToolRequest
        {
            Name = _tool.Name,
            Arguments = JsonSerializer.SerializeToElement(new
            {
                {{PARAM_NAME}} = "test_value"
            })
        };
        
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        Assert.ThrowsAsync<OperationCanceledException>(
            async () => await _tool.ExecuteAsync(request, cts.Token));
    }
}
#endif