using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using McpServer.Domain.Interfaces;
using McpServer.Domain.Models;

namespace McpServer.Application.Handlers;

/// <summary>
/// Handles {{HANDLER_DESCRIPTION}}
/// </summary>
public class {{HANDLER_NAME}}Handler : IRequestHandler<{{REQUEST_TYPE}}, {{RESPONSE_TYPE}}>
{
    private readonly ILogger<{{HANDLER_NAME}}Handler> _logger;
    private readonly {{DEPENDENCY_INTERFACE}} _dependency;

    public {{HANDLER_NAME}}Handler(
        ILogger<{{HANDLER_NAME}}Handler> logger,
        {{DEPENDENCY_INTERFACE}} dependency)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dependency = dependency ?? throw new ArgumentNullException(nameof(dependency));
    }

    /// <summary>
    /// Handles the {{REQUEST_TYPE}} request
    /// </summary>
    public async Task<{{RESPONSE_TYPE}}> HandleAsync(
        {{REQUEST_TYPE}} request, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Handling {{REQUEST_TYPE}} request: {RequestId}", request.Id);
        
        try
        {
            // Validate request
            ValidateRequest(request);
            
            // Process request
            var result = await ProcessRequestAsync(request, cancellationToken);
            
            _logger.LogInformation("Successfully handled {{REQUEST_TYPE}}: {RequestId}", request.Id);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("{{REQUEST_TYPE}} handling cancelled: {RequestId}", request.Id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling {{REQUEST_TYPE}}: {RequestId}", request.Id);
            throw new HandlerException($"Failed to handle {{REQUEST_TYPE}}", ex);
        }
    }

    private void ValidateRequest({{REQUEST_TYPE}} request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        // TODO: Add specific validation logic
        {{VALIDATION_LOGIC}}
    }

    private async Task<{{RESPONSE_TYPE}}> ProcessRequestAsync(
        {{REQUEST_TYPE}} request,
        CancellationToken cancellationToken)
    {
        // TODO: Implement processing logic
        {{PROCESSING_LOGIC}}
        
        return new {{RESPONSE_TYPE}}
        {
            Success = true,
            // TODO: Set response properties
        };
    }
}

// Unit test template
#if TEST_TEMPLATE
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace McpServer.Application.Tests.Handlers;

[TestFixture]
public class {{HANDLER_NAME}}HandlerTests
{
    private Mock<ILogger<{{HANDLER_NAME}}Handler>> _loggerMock;
    private Mock<{{DEPENDENCY_INTERFACE}}> _dependencyMock;
    private {{HANDLER_NAME}}Handler _handler;

    [SetUp]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<{{HANDLER_NAME}}Handler>>();
        _dependencyMock = new Mock<{{DEPENDENCY_INTERFACE}}>();
        _handler = new {{HANDLER_NAME}}Handler(_loggerMock.Object, _dependencyMock.Object);
    }

    [Test]
    public async Task HandleAsync_ValidRequest_ReturnsSuccessResponse()
    {
        // Arrange
        var request = new {{REQUEST_TYPE}} { /* Set properties */ };
        
        // Act
        var result = await _handler.HandleAsync(request);
        
        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public void HandleAsync_NullRequest_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _handler.HandleAsync(null!));
    }

    [Test]
    public async Task HandleAsync_OperationCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var request = new {{REQUEST_TYPE}} { /* Set properties */ };
        var cts = new CancellationTokenSource();
        cts.Cancel();
        
        // Act & Assert
        Assert.ThrowsAsync<OperationCanceledException>(
            async () => await _handler.HandleAsync(request, cts.Token));
    }
}
#endif