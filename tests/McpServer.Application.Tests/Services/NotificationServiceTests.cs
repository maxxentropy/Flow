using FluentAssertions;
using McpServer.Application.Services;
using McpServer.Domain.Protocol.Messages;
using McpServer.Domain.Transport;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace McpServer.Application.Tests.Services;

public class NotificationServiceTests
{
    private readonly Mock<ILogger<NotificationService>> _loggerMock;
    private readonly Mock<ITransport> _transportMock;
    private readonly NotificationService _notificationService;
    
    public NotificationServiceTests()
    {
        _loggerMock = new Mock<ILogger<NotificationService>>();
        _transportMock = new Mock<ITransport>();
        _notificationService = new NotificationService(_loggerMock.Object, _transportMock.Object);
    }
    
    [Fact]
    public async Task SendNotificationAsync_WithTransport_SendsNotification()
    {
        // Arrange
        var notification = new ToolsUpdatedNotification();
        
        // Act
        await _notificationService.SendNotificationAsync(notification);
        
        // Assert
        _transportMock.Verify(x => x.SendMessageAsync(
            It.Is<ToolsUpdatedNotification>(n => n.Method == "notifications/tools/updated"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
    
    [Fact]
    public async Task SendNotificationAsync_WithoutTransport_LogsWarning()
    {
        // Arrange
        var service = new NotificationService(_loggerMock.Object);
        var notification = new ToolsUpdatedNotification();
        
        // Act
        await service.SendNotificationAsync(notification);
        
        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cannot send notification")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
    
    [Fact]
    public async Task NotifyResourcesUpdatedAsync_SendsCorrectNotification()
    {
        // Act
        await _notificationService.NotifyResourcesUpdatedAsync();
        
        // Assert
        _transportMock.Verify(x => x.SendMessageAsync(
            It.Is<ResourcesUpdatedNotification>(n => n.Method == "notifications/resources/updated"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
    
    [Fact]
    public async Task NotifyResourceUpdatedAsync_SendsCorrectNotification()
    {
        // Arrange
        var uri = "file://test.txt";
        
        // Act
        await _notificationService.NotifyResourceUpdatedAsync(uri);
        
        // Assert
        _transportMock.Verify(x => x.SendMessageAsync(
            It.Is<ResourceUpdatedNotification>(n => 
                n.Method == "notifications/resources/updated" &&
                n.ResourceParams.Uri == uri),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
    
    [Fact]
    public async Task NotifyToolsUpdatedAsync_SendsCorrectNotification()
    {
        // Act
        await _notificationService.NotifyToolsUpdatedAsync();
        
        // Assert
        _transportMock.Verify(x => x.SendMessageAsync(
            It.Is<ToolsUpdatedNotification>(n => n.Method == "notifications/tools/updated"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
    
    [Fact]
    public async Task NotifyPromptsUpdatedAsync_SendsCorrectNotification()
    {
        // Act
        await _notificationService.NotifyPromptsUpdatedAsync();
        
        // Assert
        _transportMock.Verify(x => x.SendMessageAsync(
            It.Is<PromptsUpdatedNotification>(n => n.Method == "notifications/prompts/updated"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
    
    [Fact]
    public async Task NotifyProgressAsync_SendsCorrectNotification()
    {
        // Arrange
        var progressToken = "test-token";
        var progress = 50.0;
        var total = 100.0;
        var message = "Processing...";
        
        // Act
        await _notificationService.NotifyProgressAsync(progressToken, progress, total, message);
        
        // Assert
        _transportMock.Verify(x => x.SendMessageAsync(
            It.Is<ProgressNotification>(n => 
                n.Method == "notifications/progress" &&
                n.ProgressParams.ProgressToken == progressToken &&
                n.ProgressParams.Progress == progress &&
                n.ProgressParams.Total == total &&
                n.ProgressParams.Message == message),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
    
    [Fact]
    public async Task NotifyCancelledAsync_SendsCorrectNotification()
    {
        // Arrange
        var requestId = "test-request-123";
        var reason = "User cancelled";
        
        // Act
        await _notificationService.NotifyCancelledAsync(requestId, reason);
        
        // Assert
        _transportMock.Verify(x => x.SendMessageAsync(
            It.Is<CancelledNotification>(n => 
                n.Method == "notifications/cancelled" &&
                n.CancelledParams.RequestId == requestId &&
                n.CancelledParams.Reason == reason),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
    
    [Fact]
    public async Task SetTransport_UpdatesTransport()
    {
        // Arrange
        var service = new NotificationService(_loggerMock.Object);
        var newTransport = new Mock<ITransport>();
        
        // Act
        service.SetTransport(newTransport.Object);
        
        // Assert
        // Verify by sending a notification
        await service.SendNotificationAsync(new ToolsUpdatedNotification());
        newTransport.Verify(x => x.SendMessageAsync(
            It.IsAny<ToolsUpdatedNotification>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
    
    [Fact]
    public async Task SendNotificationAsync_TransportThrows_LogsErrorAndRethrows()
    {
        // Arrange
        var notification = new ToolsUpdatedNotification();
        var exception = new InvalidOperationException("Transport error");
        _transportMock.Setup(x => x.SendMessageAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);
        
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _notificationService.SendNotificationAsync(notification));
        
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error sending notification")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}