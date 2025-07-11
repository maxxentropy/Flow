using FluentAssertions;
using McpServer.Application.Server;
using McpServer.Application.Services;
using McpServer.Domain.Protocol.Messages;
using McpServer.Domain.Transport;
using McpServer.Domain.Tools;
using McpServer.Domain.Resources;
using McpServer.Domain.Prompts;
using McpServer.Domain.Connection;
using McpServer.Infrastructure.Tools;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using MServer = McpServer.Application.Server.MultiplexingMcpServer;

namespace McpServer.Application.Tests.Server;

public class McpServerNotificationTests
{
    private readonly Mock<ILogger<MServer>> _loggerMock;
    private readonly Mock<IConnectionManager> _connectionManagerMock;
    private readonly Mock<IConnectionAwareMessageRouter> _messageRouterMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly Mock<ISamplingService> _samplingServiceMock;
    private readonly Mock<ITransport> _transportMock;
    private readonly MServer _server;
    
    public McpServerNotificationTests()
    {
        _loggerMock = new Mock<ILogger<MServer>>();
        _connectionManagerMock = new Mock<IConnectionManager>();
        _messageRouterMock = new Mock<IConnectionAwareMessageRouter>();
        _notificationServiceMock = new Mock<INotificationService>();
        _samplingServiceMock = new Mock<ISamplingService>();
        _transportMock = new Mock<ITransport>();
        
        var serverInfo = new ServerInfo { Name = "Test Server", Version = "1.0.0" };
        var capabilities = new ServerCapabilities
        {
            Tools = new ToolsCapability { ListChanged = true },
            Resources = new ResourcesCapability { Subscribe = false, ListChanged = true },
            Prompts = new PromptsCapability { ListChanged = true }
        };
        
        _server = new MServer(
            _loggerMock.Object,
            _connectionManagerMock.Object,
            _messageRouterMock.Object,
            _notificationServiceMock.Object,
            _samplingServiceMock.Object,
            serverInfo,
            capabilities);
    }
    
    [Fact]
    public async Task StartAsync_SetsTransportInNotificationService()
    {
        // Arrange
        var realNotificationService = new NotificationService(Mock.Of<ILogger<NotificationService>>());
        var server = new MServer(
            _loggerMock.Object,
            _connectionManagerMock.Object,
            _messageRouterMock.Object,
            realNotificationService,
            _samplingServiceMock.Object,
            new ServerInfo { Name = "Test", Version = "1.0" },
            new ServerCapabilities());
        
        // Act
        await server.StartAsync(_transportMock.Object);
        
        // Assert
        // Verify by checking that notification service can send notifications
        await realNotificationService.SendNotificationAsync(new ToolsUpdatedNotification());
        _transportMock.Verify(x => x.SendMessageAsync(
            It.IsAny<ToolsUpdatedNotification>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
    
    [Fact]
    public async Task RegisterTool_WhenServerRunning_SendsNotification()
    {
        // Arrange
        await _server.StartAsync(_transportMock.Object);
        var tool = new EchoTool(Mock.Of<ILogger<EchoTool>>());
        
        // Act
        _server.RegisterTool(tool);
        
        // Wait a bit for async notification
        await Task.Delay(100);
        
        // Assert
        _notificationServiceMock.Verify(x => x.NotifyToolsUpdatedAsync(
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
    
    [Fact]
    public void RegisterTool_WhenServerNotRunning_DoesNotSendNotification()
    {
        // Arrange
        var tool = new EchoTool(Mock.Of<ILogger<EchoTool>>());
        
        // Act
        _server.RegisterTool(tool);
        
        // Assert
        _notificationServiceMock.Verify(x => x.NotifyToolsUpdatedAsync(
            It.IsAny<CancellationToken>()),
            Times.Never);
    }
    
    [Fact]
    public async Task RegisterTool_WhenListChangedFalse_DoesNotSendNotification()
    {
        // Arrange
        var capabilities = new ServerCapabilities
        {
            Tools = new ToolsCapability { ListChanged = false }
        };
        var server = new MServer(
            _loggerMock.Object,
            _connectionManagerMock.Object,
            _messageRouterMock.Object,
            _notificationServiceMock.Object,
            _samplingServiceMock.Object,
            new ServerInfo { Name = "Test", Version = "1.0" },
            capabilities);
        
        await server.StartAsync(_transportMock.Object);
        var tool = new EchoTool(Mock.Of<ILogger<EchoTool>>());
        
        // Act
        server.RegisterTool(tool);
        await Task.Delay(100);
        
        // Assert
        _notificationServiceMock.Verify(x => x.NotifyToolsUpdatedAsync(
            It.IsAny<CancellationToken>()),
            Times.Never);
    }
    
    [Fact]
    public async Task RegisterResourceProvider_WhenServerRunning_SendsNotification()
    {
        // Arrange
        await _server.StartAsync(_transportMock.Object);
        var provider = Mock.Of<IResourceProvider>();
        
        // Act
        _server.RegisterResourceProvider(provider);
        
        // Wait a bit for async notification
        await Task.Delay(100);
        
        // Assert
        _notificationServiceMock.Verify(x => x.NotifyResourcesUpdatedAsync(
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
    
    [Fact]
    public async Task RegisterPromptProvider_WhenServerRunning_SendsNotification()
    {
        // Arrange
        await _server.StartAsync(_transportMock.Object);
        var provider = Mock.Of<IPromptProvider>();
        
        // Act
        _server.RegisterPromptProvider(provider);
        
        // Wait a bit for async notification
        await Task.Delay(100);
        
        // Assert
        _notificationServiceMock.Verify(x => x.NotifyPromptsUpdatedAsync(
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
    
    [Fact]
    public void RegisterMultipleTools_StoresAllTools()
    {
        // Arrange
        var tool1 = new EchoTool(Mock.Of<ILogger<EchoTool>>());
        var tool2 = new CalculatorTool(Mock.Of<ILogger<CalculatorTool>>());
        
        // Act
        _server.RegisterTool(tool1);
        _server.RegisterTool(tool2);
        
        // Assert
        var tools = _server.GetTools();
        tools.Should().HaveCount(2);
        tools.Should().ContainKey("echo");
        tools.Should().ContainKey("calculator");
    }
    
    [Fact]
    public void RegisterDuplicateTool_ThrowsException()
    {
        // Arrange
        var tool1 = new EchoTool(Mock.Of<ILogger<EchoTool>>());
        var tool2 = new EchoTool(Mock.Of<ILogger<EchoTool>>());
        _server.RegisterTool(tool1);
        
        // Act & Assert
        var act = () => _server.RegisterTool(tool2);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Tool 'echo' is already registered");
    }
}