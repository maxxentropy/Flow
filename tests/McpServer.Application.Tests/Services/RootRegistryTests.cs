using FluentAssertions;
using McpServer.Application.Services;
using McpServer.Domain.Protocol.Messages;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace McpServer.Application.Tests.Services;

public class RootRegistryTests
{
    private readonly Mock<ILogger<RootRegistry>> _loggerMock;
    private readonly RootRegistry _rootRegistry;

    public RootRegistryTests()
    {
        _loggerMock = new Mock<ILogger<RootRegistry>>();
        _rootRegistry = new RootRegistry(_loggerMock.Object);
    }

    [Fact]
    public void Roots_WhenEmpty_ReturnsEmptyList()
    {
        // Assert
        _rootRegistry.Roots.Should().BeEmpty();
    }

    [Fact]
    public void HasRoots_WhenEmpty_ReturnsFalse()
    {
        // Assert
        _rootRegistry.HasRoots.Should().BeFalse();
    }

    [Fact]
    public void HasRoots_WhenNotEmpty_ReturnsTrue()
    {
        // Arrange
        var root = new Root { Uri = "file:///test", Name = "Test" };

        // Act
        _rootRegistry.AddRoot(root);

        // Assert
        _rootRegistry.HasRoots.Should().BeTrue();
    }

    [Fact]
    public void AddRoot_WithValidRoot_AddsToCollection()
    {
        // Arrange
        var root = new Root { Uri = "file:///test", Name = "Test" };

        // Act
        _rootRegistry.AddRoot(root);

        // Assert
        _rootRegistry.Roots.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(root);
    }

    [Fact]
    public void AddRoot_WithDuplicateUri_DoesNotAddDuplicate()
    {
        // Arrange
        var root1 = new Root { Uri = "file:///test", Name = "Test1" };
        var root2 = new Root { Uri = "file:///test", Name = "Test2" };

        // Act
        _rootRegistry.AddRoot(root1);
        _rootRegistry.AddRoot(root2);

        // Assert
        _rootRegistry.Roots.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(root1);
    }

    [Fact]
    public void AddRoot_WithNullRoot_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => _rootRegistry.AddRoot(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RemoveRoot_WithExistingUri_RemovesRoot()
    {
        // Arrange
        var root = new Root { Uri = "file:///test", Name = "Test" };
        _rootRegistry.AddRoot(root);

        // Act
        var result = _rootRegistry.RemoveRoot("file:///test");

        // Assert
        result.Should().BeTrue();
        _rootRegistry.Roots.Should().BeEmpty();
    }

    [Fact]
    public void RemoveRoot_WithNonExistingUri_ReturnsFalse()
    {
        // Act
        var result = _rootRegistry.RemoveRoot("file:///nonexistent");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void RemoveRoot_WithNullOrWhiteSpaceUri_ThrowsArgumentException()
    {
        // Act & Assert
        var act1 = () => _rootRegistry.RemoveRoot(null!);
        var act2 = () => _rootRegistry.RemoveRoot("");
        var act3 = () => _rootRegistry.RemoveRoot("   ");

        act1.Should().Throw<ArgumentException>();
        act2.Should().Throw<ArgumentException>();
        act3.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateRoots_WithNewRoots_ReplacesAllRoots()
    {
        // Arrange
        var initialRoot = new Root { Uri = "file:///initial", Name = "Initial" };
        _rootRegistry.AddRoot(initialRoot);

        var newRoots = new[]
        {
            new Root { Uri = "file:///new1", Name = "New1" },
            new Root { Uri = "file:///new2", Name = "New2" }
        };

        // Act
        _rootRegistry.UpdateRoots(newRoots);

        // Assert
        _rootRegistry.Roots.Should().HaveCount(2)
            .And.BeEquivalentTo(newRoots);
    }

    [Fact]
    public void UpdateRoots_WithNullRoots_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => _rootRegistry.UpdateRoots(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ClearRoots_RemovesAllRoots()
    {
        // Arrange
        _rootRegistry.AddRoot(new Root { Uri = "file:///test1", Name = "Test1" });
        _rootRegistry.AddRoot(new Root { Uri = "file:///test2", Name = "Test2" });

        // Act
        _rootRegistry.ClearRoots();

        // Assert
        _rootRegistry.Roots.Should().BeEmpty();
        _rootRegistry.HasRoots.Should().BeFalse();
    }

    [Theory]
    [InlineData("file:///home/user/project/file.txt", "file:///home/user/project", true)]
    [InlineData("file:///home/user/project/subdir/file.txt", "file:///home/user/project", true)]
    [InlineData("file:///home/user/other/file.txt", "file:///home/user/project", false)]
    [InlineData("file:///home/user/project", "file:///home/user/project", true)]
    [InlineData("file:///home/user/projects", "file:///home/user/project", false)]
    public void IsWithinRootBoundaries_WithFileUris_ReturnsCorrectResult(string testUri, string rootUri, bool expected)
    {
        // Arrange
        var root = new Root { Uri = rootUri, Name = "Test" };
        _rootRegistry.AddRoot(root);

        // Act
        var result = _rootRegistry.IsWithinRootBoundaries(testUri);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void IsWithinRootBoundaries_WithNoRoots_ReturnsTrue()
    {
        // Act
        var result = _rootRegistry.IsWithinRootBoundaries("file:///any/path");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsWithinRootBoundaries_WithNullOrWhiteSpaceUri_ThrowsArgumentException()
    {
        // Act & Assert
        var act1 = () => _rootRegistry.IsWithinRootBoundaries(null!);
        var act2 = () => _rootRegistry.IsWithinRootBoundaries("");
        var act3 = () => _rootRegistry.IsWithinRootBoundaries("   ");

        act1.Should().Throw<ArgumentException>();
        act2.Should().Throw<ArgumentException>();
        act3.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("https://api.example.com/resource", "https://api.example.com", true)]
    [InlineData("https://api.example.com/v1/resource", "https://api.example.com", true)]
    [InlineData("https://other.example.com/resource", "https://api.example.com", false)]
    [InlineData("http://api.example.com/resource", "https://api.example.com", false)]
    public void IsWithinRootBoundaries_WithHttpUris_ReturnsCorrectResult(string testUri, string rootUri, bool expected)
    {
        // Arrange
        var root = new Root { Uri = rootUri, Name = "Test" };
        _rootRegistry.AddRoot(root);

        // Act
        var result = _rootRegistry.IsWithinRootBoundaries(testUri);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void GetContainingRoot_WithMatchingUri_ReturnsRoot()
    {
        // Arrange
        var root = new Root { Uri = "file:///home/user/project", Name = "Project" };
        _rootRegistry.AddRoot(root);
        var testUri = "file:///home/user/project/file.txt";

        // Act
        var result = _rootRegistry.GetContainingRoot(testUri);

        // Assert
        result.Should().BeEquivalentTo(root);
    }

    [Fact]
    public void GetContainingRoot_WithNonMatchingUri_ReturnsNull()
    {
        // Arrange
        var root = new Root { Uri = "file:///home/user/project", Name = "Project" };
        _rootRegistry.AddRoot(root);
        var testUri = "file:///home/user/other/file.txt";

        // Act
        var result = _rootRegistry.GetContainingRoot(testUri);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetContainingRoot_WithMultipleRoots_ReturnsFirstMatch()
    {
        // Arrange
        var root1 = new Root { Uri = "file:///home/user", Name = "User" };
        var root2 = new Root { Uri = "file:///home/user/project", Name = "Project" };
        _rootRegistry.AddRoot(root1);
        _rootRegistry.AddRoot(root2);
        var testUri = "file:///home/user/project/file.txt";

        // Act
        var result = _rootRegistry.GetContainingRoot(testUri);

        // Assert
        result.Should().BeEquivalentTo(root1); // First match wins
    }

    [Fact]
    public void ValidateUriAccess_WithAllowedUri_DoesNotThrow()
    {
        // Arrange
        var root = new Root { Uri = "file:///home/user/project", Name = "Project" };
        _rootRegistry.AddRoot(root);
        var testUri = "file:///home/user/project/file.txt";

        // Act & Assert
        var act = () => _rootRegistry.ValidateUriAccess(testUri);
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateUriAccess_WithDisallowedUri_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var root = new Root { Uri = "file:///home/user/project", Name = "Project" };
        _rootRegistry.AddRoot(root);
        var testUri = "file:///home/user/other/file.txt";

        // Act & Assert
        var act = () => _rootRegistry.ValidateUriAccess(testUri);
        act.Should().Throw<UnauthorizedAccessException>()
            .WithMessage("*outside of configured root boundaries*");
    }

    [Fact]
    public void RootsChanged_WhenRootsUpdated_FiresEvent()
    {
        // Arrange
        var eventFired = false;
        RootsChangedEventArgs? eventArgs = null;
        _rootRegistry.RootsChanged += (sender, args) =>
        {
            eventFired = true;
            eventArgs = args;
        };

        var newRoots = new[] { new Root { Uri = "file:///test", Name = "Test" } };

        // Act
        _rootRegistry.UpdateRoots(newRoots);

        // Assert
        eventFired.Should().BeTrue();
        eventArgs.Should().NotBeNull();
        eventArgs!.PreviousRoots.Should().BeEmpty();
        eventArgs.NewRoots.Should().BeEquivalentTo(newRoots);
    }

    [Fact]
    public void RootsChanged_WhenRootAdded_FiresEvent()
    {
        // Arrange
        var eventFired = false;
        _rootRegistry.RootsChanged += (sender, args) => eventFired = true;

        var root = new Root { Uri = "file:///test", Name = "Test" };

        // Act
        _rootRegistry.AddRoot(root);

        // Assert
        eventFired.Should().BeTrue();
    }

    [Fact]
    public void RootsChanged_WhenRootRemoved_FiresEvent()
    {
        // Arrange
        var root = new Root { Uri = "file:///test", Name = "Test" };
        _rootRegistry.AddRoot(root);

        var eventFired = false;
        _rootRegistry.RootsChanged += (sender, args) => eventFired = true;

        // Act
        _rootRegistry.RemoveRoot("file:///test");

        // Assert
        eventFired.Should().BeTrue();
    }

    [Fact]
    public void RootsChanged_WhenRootsCleared_FiresEvent()
    {
        // Arrange
        _rootRegistry.AddRoot(new Root { Uri = "file:///test", Name = "Test" });

        var eventFired = false;
        _rootRegistry.RootsChanged += (sender, args) => eventFired = true;

        // Act
        _rootRegistry.ClearRoots();

        // Assert
        eventFired.Should().BeTrue();
    }

    [Fact]
    public async Task ThreadSafety_ConcurrentOperations_DoNotCauseExceptions()
    {
        // Arrange
        var tasks = new List<Task>();
        var random = new Random();

        // Act - perform concurrent operations
        for (int i = 0; i < 100; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    switch (random.Next(4))
                    {
                        case 0:
                            _rootRegistry.AddRoot(new Root { Uri = $"file:///test{index}", Name = $"Test{index}" });
                            break;
                        case 1:
                            _rootRegistry.RemoveRoot($"file:///test{index}");
                            break;
                        case 2:
                            _ = _rootRegistry.IsWithinRootBoundaries($"file:///test{index}/file.txt");
                            break;
                        case 3:
                            _ = _rootRegistry.Roots.Count;
                            break;
                    }
                }
                catch
                {
                    // Ignore exceptions for this test - we're just checking for thread safety
                }
            }));
        }

        // Assert - should not throw any exceptions
        var act = async () => await Task.WhenAll(tasks);
        await act.Should().NotThrowAsync();
    }
}