using FluentAssertions;
using McpServer.Application.Resources;
using Xunit;

namespace McpServer.Application.Tests.Resources;

public class ResourceTemplateTests
{
    [Fact]
    public void Constructor_Should_CreateValidTemplate()
    {
        // Arrange & Act
        var template = new ResourceTemplate(
            "api://users/{userId}/posts/{postId}",
            "User Post",
            "A specific post by a user",
            "application/json"
        );
        
        // Assert
        template.UriPattern.Should().Be("api://users/{userId}/posts/{postId}");
        template.Name.Should().Be("User Post");
        template.Description.Should().Be("A specific post by a user");
        template.MimeType.Should().Be("application/json");
    }
    
    [Theory]
    [InlineData("api://users/123/posts/456", true)]
    [InlineData("api://users/abc/posts/def", true)]
    [InlineData("api://users/123/posts", false)]
    [InlineData("api://users/123/posts/456/comments", false)]
    [InlineData("http://users/123/posts/456", false)]
    public void Matches_Should_CorrectlyIdentifyMatchingUris(string uri, bool expectedMatch)
    {
        // Arrange
        var template = new ResourceTemplate("api://users/{userId}/posts/{postId}", "Test");
        
        // Act
        var matches = template.Matches(uri);
        
        // Assert
        matches.Should().Be(expectedMatch);
    }
    
    [Fact]
    public void ExtractParameters_Should_ExtractAllParameters()
    {
        // Arrange
        var template = new ResourceTemplate("db://{database}/tables/{table}/columns/{column}", "Test");
        var uri = "db://customers/tables/users/columns/email";
        
        // Act
        var parameters = template.ExtractParameters(uri);
        
        // Assert
        parameters.Should().HaveCount(3);
        parameters["database"].Should().Be("customers");
        parameters["table"].Should().Be("users");
        parameters["column"].Should().Be("email");
    }
    
    [Fact]
    public void ExtractParameters_Should_HandleUrlEncodedValues()
    {
        // Arrange
        var template = new ResourceTemplate("api://search/{query}", "Search");
        var uri = "api://search/hello%20world";
        
        // Act
        var parameters = template.ExtractParameters(uri);
        
        // Assert
        parameters["query"].Should().Be("hello world");
    }
    
    [Fact]
    public void ExtractParameters_Should_ReturnEmptyForNonMatchingUri()
    {
        // Arrange
        var template = new ResourceTemplate("api://users/{id}", "Test");
        var uri = "api://products/123";
        
        // Act
        var parameters = template.ExtractParameters(uri);
        
        // Assert
        parameters.Should().BeEmpty();
    }
    
    [Fact]
    public void GenerateUri_Should_CreateValidUri()
    {
        // Arrange
        var template = new ResourceTemplate("api://users/{userId}/posts/{postId}", "Test");
        var parameters = new Dictionary<string, string>
        {
            ["userId"] = "123",
            ["postId"] = "456"
        };
        
        // Act
        var uri = template.GenerateUri(parameters);
        
        // Assert
        uri.Should().Be("api://users/123/posts/456");
    }
    
    [Fact]
    public void GenerateUri_Should_UrlEncodeValues()
    {
        // Arrange
        var template = new ResourceTemplate("api://search/{query}", "Search");
        var parameters = new Dictionary<string, string>
        {
            ["query"] = "hello world"
        };
        
        // Act
        var uri = template.GenerateUri(parameters);
        
        // Assert
        uri.Should().Be("api://search/hello%20world");
    }
    
    [Fact]
    public void GenerateUri_Should_ThrowForMissingParameter()
    {
        // Arrange
        var template = new ResourceTemplate("api://users/{userId}/posts/{postId}", "Test");
        var parameters = new Dictionary<string, string>
        {
            ["userId"] = "123"
            // Missing postId
        };
        
        // Act
        var act = () => template.GenerateUri(parameters);
        
        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Missing required parameter: postId");
    }
    
    [Fact]
    public void ResourceTemplateBuilder_Should_CreateTemplate()
    {
        // Arrange & Act
        var template = new ResourceTemplateBuilder()
            .WithUriPattern("api://resources/{id}")
            .WithName("Resource")
            .WithDescription("A resource")
            .WithMimeType("application/json")
            .Build();
        
        // Assert
        template.UriPattern.Should().Be("api://resources/{id}");
        template.Name.Should().Be("Resource");
        template.Description.Should().Be("A resource");
        template.MimeType.Should().Be("application/json");
    }
    
    [Fact]
    public void ResourceTemplateBuilder_Should_ThrowForMissingPattern()
    {
        // Arrange
        var builder = new ResourceTemplateBuilder()
            .WithName("Test");
        
        // Act
        var act = () => builder.Build();
        
        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("URI pattern is required");
    }
    
    [Fact]
    public void ResourceTemplateBuilder_Should_ThrowForMissingName()
    {
        // Arrange
        var builder = new ResourceTemplateBuilder()
            .WithUriPattern("api://test");
        
        // Act
        var act = () => builder.Build();
        
        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Template name is required");
    }
}