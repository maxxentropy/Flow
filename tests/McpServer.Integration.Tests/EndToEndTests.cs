using FluentAssertions;
using McpServer.Abstractions;
using McpServer.Application.Server;
using McpServer.Domain.Tools;
using McpServer.Infrastructure.Tools;
using McpServer.Infrastructure.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using System.Text.Json;
using Xunit;

namespace McpServer.Integration.Tests;

public class EndToEndTests
{
    [Fact]
    public void ServiceRegistration_Should_Resolve_All_Dependencies()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddMcpServer();
        services.AddMcpTools();

        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        serviceProvider.GetService<IMcpServer>().Should().NotBeNull();
        serviceProvider.GetService<IMessageRouter>().Should().NotBeNull();
        serviceProvider.GetService<StdioTransport>().Should().NotBeNull();
        serviceProvider.GetService<SseTransport>().Should().NotBeNull();
    }

    [Fact]
    public async Task McpServer_Should_Initialize_Successfully()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddMcpServer();
        services.AddMcpTools();

        var serviceProvider = services.BuildServiceProvider();
        var server = serviceProvider.GetRequiredService<IMcpServer>();
        var messageRouter = serviceProvider.GetRequiredService<IMessageRouter>();

        // Register tools
        var tools = serviceProvider.GetServices<ITool>();
        foreach (var tool in tools)
        {
            server.RegisterTool(tool);
        }

        // Act - Send initialize request
        var initRequest = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "initialize",
            @params = new
            {
                protocolVersion = "0.1.0",
                capabilities = new { },
                clientInfo = new
                {
                    name = "Test Client",
                    version = "1.0.0"
                }
            }
        });

        var response = await messageRouter.RouteMessageAsync(initRequest);

        // Assert
        response.Should().NotBeNull();
        var json = JsonSerializer.Serialize(response);
        json.Should().Contain("\"protocolVersion\":\"0.1.0\"");
        json.Should().Contain("\"serverInfo\"");
        
        server.IsInitialized.Should().BeTrue();
    }

    [Fact]
    public async Task Tools_Should_Be_Listed_After_Initialization()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddMcpServer();
        services.AddMcpTools();

        var serviceProvider = services.BuildServiceProvider();
        var server = serviceProvider.GetRequiredService<IMcpServer>();
        var messageRouter = serviceProvider.GetRequiredService<IMessageRouter>();

        // Register tools
        var tools = serviceProvider.GetServices<ITool>();
        foreach (var tool in tools)
        {
            server.RegisterTool(tool);
        }

        // Initialize server
        var initRequest = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "initialize",
            @params = new
            {
                protocolVersion = "0.1.0",
                capabilities = new { },
                clientInfo = new
                {
                    name = "Test Client",
                    version = "1.0.0"
                }
            }
        });

        await messageRouter.RouteMessageAsync(initRequest);

        // Act - List tools
        var listRequest = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 2,
            method = "tools/list"
        });

        var response = await messageRouter.RouteMessageAsync(listRequest);

        // Assert
        response.Should().NotBeNull();
        var json = JsonSerializer.Serialize(response);
        json.Should().Contain("\"tools\"");
        json.Should().Contain("\"echo\"");
        json.Should().Contain("\"calculator\"");
        json.Should().Contain("\"datetime\"");
    }
}

public class ConfigurationBuilder : IConfigurationBuilder
{
    private readonly List<IConfigurationSource> _sources = new();

    public IList<IConfigurationSource> Sources => _sources;

    public IDictionary<string, object> Properties { get; } = new Dictionary<string, object>();

    public IConfigurationBuilder Add(IConfigurationSource source)
    {
        _sources.Add(source);
        return this;
    }

    public IConfigurationRoot Build()
    {
        return new ConfigurationRoot(_sources);
    }
}

public class ConfigurationRoot : IConfigurationRoot
{
    private readonly List<IConfigurationProvider> _providers = new();

    public ConfigurationRoot(IList<IConfigurationSource> sources)
    {
        foreach (var source in sources)
        {
            var provider = source.Build((IConfigurationBuilder)null!);
            _providers.Add(provider);
        }
    }

    public string? this[string key]
    {
        get => GetSection(key).Value;
        set => throw new NotSupportedException();
    }

    public IEnumerable<IConfigurationProvider> Providers => _providers;

    public IEnumerable<IConfigurationSection> GetChildren()
    {
        return Enumerable.Empty<IConfigurationSection>();
    }

    public IChangeToken GetReloadToken()
    {
        return new ChangeToken();
    }

    public IConfigurationSection GetSection(string key)
    {
        return new ConfigurationSection(this, key);
    }

    public void Reload()
    {
    }
}

public class ConfigurationSection : IConfigurationSection
{
    public ConfigurationSection(IConfigurationRoot root, string path)
    {
        Path = path;
        Key = path.Split(':').Last();
    }

    public string? this[string key]
    {
        get => null;
        set => throw new NotSupportedException();
    }

    public string Key { get; }
    public string Path { get; }
    public string? Value { get; set; }

    public IEnumerable<IConfigurationSection> GetChildren()
    {
        return Enumerable.Empty<IConfigurationSection>();
    }

    public IChangeToken GetReloadToken()
    {
        return new ChangeToken();
    }

    public IConfigurationSection GetSection(string key)
    {
        return new ConfigurationSection(null!, Path + ":" + key);
    }
}

public class ChangeToken : IChangeToken
{
    public bool HasChanged => false;
    public bool ActiveChangeCallbacks => false;
    public IDisposable RegisterChangeCallback(Action<object?> callback, object? state) => new DisposableAction();
}

public class DisposableAction : IDisposable
{
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}