using FluentAssertions;
using McpServer.Domain.Resources;
using McpServer.Domain.Exceptions;
using McpServer.Infrastructure.Resources;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Json;
using Xunit;

namespace McpServer.Infrastructure.Tests.Resources;

public class DatabaseSchemaResourceProviderTests
{
    private readonly Mock<ILogger<DatabaseSchemaResourceProvider>> _logger;
    private readonly DatabaseSchemaResourceProvider _provider;
    
    public DatabaseSchemaResourceProviderTests()
    {
        _logger = new Mock<ILogger<DatabaseSchemaResourceProvider>>();
        var options = Options.Create(new DatabaseSchemaResourceOptions
        {
            Databases = new List<string> { "customers", "analytics" }
        });
        
        _provider = new DatabaseSchemaResourceProvider(_logger.Object, options);
    }
    
    [Fact]
    public void Constructor_Should_RegisterTemplates()
    {
        // Assert
        _provider.Templates.Should().HaveCount(3);
        _provider.Templates.Should().Contain(t => t.Name == "Database Schema");
        _provider.Templates.Should().Contain(t => t.Name == "Table Schema");
        _provider.Templates.Should().Contain(t => t.Name == "Column Details");
    }
    
    [Fact]
    public async Task ListResourcesAsync_Should_ReturnAllAvailableResources()
    {
        // Act
        var resources = await _provider.ListResourcesAsync();
        
        // Assert
        var resourceList = resources.ToList();
        resourceList.Should().NotBeEmpty();
        
        // Should have database schemas
        resourceList.Should().Contain(r => r.Uri == "db://customers/schema");
        resourceList.Should().Contain(r => r.Uri == "db://analytics/schema");
        
        // Should have table schemas
        resourceList.Should().Contain(r => r.Uri == "db://customers/tables/users");
        resourceList.Should().Contain(r => r.Uri == "db://customers/tables/orders");
        
        // Should have column details
        resourceList.Should().Contain(r => r.Uri == "db://customers/tables/users/columns/email");
    }
    
    [Fact]
    public async Task ReadResourceAsync_DatabaseSchema_Should_ReturnCorrectContent()
    {
        // Act
        var content = await _provider.ReadResourceAsync("db://customers/schema");
        
        // Assert
        content.Should().NotBeNull();
        content.Uri.Should().Be("db://customers/schema");
        content.MimeType.Should().Be("application/json");
        content.Text.Should().NotBeNullOrEmpty();
        
        // Verify JSON structure
        var json = JsonDocument.Parse(content.Text!);
        json.RootElement.GetProperty("database").GetString().Should().Be("customers");
        
        var tables = json.RootElement.GetProperty("tables").EnumerateArray().ToList();
        tables.Should().HaveCount(3);
        tables[0].GetProperty("name").GetString().Should().Be("users");
    }
    
    [Fact]
    public async Task ReadResourceAsync_TableSchema_Should_ReturnTableDetails()
    {
        // Act
        var content = await _provider.ReadResourceAsync("db://customers/tables/users");
        
        // Assert
        content.Should().NotBeNull();
        content.Uri.Should().Be("db://customers/tables/users");
        
        var json = JsonDocument.Parse(content.Text!);
        json.RootElement.GetProperty("table").GetString().Should().Be("users");
        
        var columns = json.RootElement.GetProperty("columns").EnumerateArray().ToList();
        columns.Should().Contain(c => c.GetProperty("name").GetString() == "id");
        columns.Should().Contain(c => c.GetProperty("name").GetString() == "email");
    }
    
    [Fact]
    public async Task ReadResourceAsync_ColumnDetails_Should_ReturnColumnInfo()
    {
        // Act
        var content = await _provider.ReadResourceAsync("db://customers/tables/users/columns/id");
        
        // Assert
        content.Should().NotBeNull();
        
        var json = JsonDocument.Parse(content.Text!);
        json.RootElement.GetProperty("column").GetString().Should().Be("id");
        json.RootElement.GetProperty("type").GetString().Should().Be("INTEGER");
        json.RootElement.GetProperty("isPrimaryKey").GetBoolean().Should().BeTrue();
        json.RootElement.GetProperty("nullable").GetBoolean().Should().BeFalse();
    }
    
    [Fact]
    public async Task ReadResourceAsync_NonExistentDatabase_Should_ThrowException()
    {
        // Act
        var act = () => _provider.ReadResourceAsync("db://nonexistent/schema");
        
        // Assert
        await act.Should().ThrowAsync<ResourceNotFoundException>()
            .WithMessage("Database 'nonexistent' not found");
    }
    
    [Fact]
    public async Task ReadResourceAsync_NonExistentTable_Should_ThrowException()
    {
        // Act
        var act = () => _provider.ReadResourceAsync("db://customers/tables/nonexistent");
        
        // Assert
        await act.Should().ThrowAsync<ResourceNotFoundException>()
            .WithMessage("Table 'nonexistent' not found in database 'customers'");
    }
    
    [Fact]
    public async Task ReadResourceAsync_NoMatchingTemplate_Should_ThrowException()
    {
        // Act
        var act = () => _provider.ReadResourceAsync("invalid://uri");
        
        // Assert
        await act.Should().ThrowAsync<ResourceNotFoundException>()
            .WithMessage("No template matches URI: invalid://uri");
    }
    
    [Fact]
    public async Task ListTemplateInstancesAsync_Should_ReturnAllPossibleInstances()
    {
        // Act
        var instances = await _provider.ListTemplateInstancesAsync();
        
        // Assert
        var instanceList = instances.ToList();
        
        // Database schema instances
        instanceList.Should().Contain(i => 
            i.Template.Name == "Database Schema" && 
            i.Parameters["database"] == "customers");
        
        // Table schema instances
        instanceList.Should().Contain(i => 
            i.Template.Name == "Table Schema" && 
            i.Parameters["database"] == "customers" &&
            i.Parameters["table"] == "users");
        
        // Column detail instances
        instanceList.Should().Contain(i => 
            i.Template.Name == "Column Details" && 
            i.Parameters["database"] == "customers" &&
            i.Parameters["table"] == "users" &&
            i.Parameters["column"] == "email");
    }
    
    [Fact]
    public void SubscribeToResourceAsync_Should_ThrowNotSupported()
    {
        // Arrange
        var observer = new Mock<IResourceObserver>();
        
        // Act
        var act = () => _provider.SubscribeToResourceAsync("db://test", observer.Object);
        
        // Assert
        act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("This provider does not support resource subscriptions");
    }
}