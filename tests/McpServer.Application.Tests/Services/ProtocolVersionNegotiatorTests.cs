using FluentAssertions;
using McpServer.Application.Services;
using McpServer.Domain.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace McpServer.Application.Tests.Services;

public class ProtocolVersionNegotiatorTests
{
    private readonly Mock<ILogger<ProtocolVersionNegotiator>> _logger;
    private readonly ProtocolVersionNegotiator _negotiator;
    private readonly ProtocolVersionConfiguration _configuration;

    public ProtocolVersionNegotiatorTests()
    {
        _logger = new Mock<ILogger<ProtocolVersionNegotiator>>();
        _configuration = new ProtocolVersionConfiguration
        {
            SupportedVersions = ["0.1.0", "0.2.0", "1.0.0", "1.1.0"],
            CurrentVersion = "1.0.0",
            AllowBackwardCompatibility = true,
            LogNegotiationDetails = true
        };
        _negotiator = new ProtocolVersionNegotiator(_logger.Object, Options.Create(_configuration));
    }

    [Fact]
    public void Constructor_Should_ParseAndSortVersions()
    {
        // Assert
        _negotiator.SupportedVersions.Should().HaveCount(4);
        _negotiator.SupportedVersions[0].Version.Should().Be("1.1.0"); // Sorted newest first
        _negotiator.SupportedVersions[1].Version.Should().Be("1.0.0");
        _negotiator.SupportedVersions[2].Version.Should().Be("0.2.0");
        _negotiator.SupportedVersions[3].Version.Should().Be("0.1.0");
        _negotiator.CurrentVersion.Version.Should().Be("1.0.0");
    }

    [Theory]
    [InlineData("0.1.0", "0.1.0")] // Exact match
    [InlineData("0.2.0", "0.2.0")] // Exact match
    [InlineData("1.0.0", "1.0.0")] // Exact match
    [InlineData("1.1.0", "1.1.0")] // Exact match
    public void NegotiateVersion_ExactMatch_Should_ReturnRequestedVersion(string clientVersion, string expectedVersion)
    {
        // Act
        var result = _negotiator.NegotiateVersion(clientVersion);

        // Assert
        result.Version.Should().Be(expectedVersion);
    }

    [Theory]
    [InlineData("0.1.1", "0.1.0")] // Client has patch version, server supports same minor with lower patch
    [InlineData("0.2.5", "0.2.0")] // Client has patch version, server supports exact minor
    [InlineData("1.0.1", "1.0.0")] // Client has patch version, server supports exact minor with lower patch
    [InlineData("0.0.1", "0.1.0")] // Client has lower minor, server supports higher minor
    [InlineData("1.2.0", "1.1.0")] // Client has higher minor than any server minor
    public void NegotiateVersion_BackwardCompatible_Should_ReturnCompatibleVersion(string clientVersion, string expectedVersion)
    {
        // Act
        var result = _negotiator.NegotiateVersion(clientVersion);

        // Assert
        result.Version.Should().Be(expectedVersion);
    }

    [Theory]
    [InlineData("2.0.0")] // Major version not supported
    [InlineData("3.0.0")] // Major version not supported
    public void NegotiateVersion_IncompatibleVersion_Should_ThrowException(string clientVersion)
    {
        // Act & Assert
        var act = () => _negotiator.NegotiateVersion(clientVersion);
        act.Should().Throw<ProtocolVersionException>()
            .WithMessage($"*{clientVersion}*")
            .Which.ClientVersion.Should().Be(clientVersion);
    }

    [Theory]
    [InlineData("invalid")] // Invalid format
    [InlineData("1.0")] // Missing patch
    [InlineData("1.0.0.0")] // Too many parts
    [InlineData("")] // Empty
    public void NegotiateVersion_InvalidFormat_Should_ThrowException(string clientVersion)
    {
        // Act & Assert
        var act = () => _negotiator.NegotiateVersion(clientVersion);
        act.Should().Throw<ProtocolVersionException>();
    }

    [Theory]
    [InlineData("0.1.0", true)]
    [InlineData("1.0.0", true)]
    [InlineData("0.1.1", true)] // Backward compatible
    [InlineData("1.0.5", true)] // Backward compatible
    [InlineData("2.0.0", false)] // Not supported
    [InlineData("invalid", false)] // Invalid format
    public void IsVersionSupported_Should_ReturnCorrectResult(string version, bool expected)
    {
        // Act
        var result = _negotiator.IsVersionSupported(version);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void TryNegotiateVersion_ValidVersion_Should_ReturnTrue()
    {
        // Act
        var result = _negotiator.TryNegotiateVersion("1.0.0", out var negotiated);

        // Assert
        result.Should().BeTrue();
        negotiated.Should().NotBeNull();
        negotiated!.Version.Should().Be("1.0.0");
    }

    [Fact]
    public void TryNegotiateVersion_InvalidVersion_Should_ReturnFalse()
    {
        // Act
        var result = _negotiator.TryNegotiateVersion("invalid", out var negotiated);

        // Assert
        result.Should().BeFalse();
        negotiated.Should().BeNull();
    }

    [Fact]
    public void NegotiateVersion_WithBackwardCompatibilityDisabled_Should_RequireExactMatch()
    {
        // Arrange
        var strictConfig = new ProtocolVersionConfiguration
        {
            SupportedVersions = ["1.0.0"],
            CurrentVersion = "1.0.0",
            AllowBackwardCompatibility = false
        };
        var strictNegotiator = new ProtocolVersionNegotiator(_logger.Object, Options.Create(strictConfig));

        // Act & Assert
        var act = () => strictNegotiator.NegotiateVersion("1.0.1");
        act.Should().Throw<ProtocolVersionException>();
    }

    [Fact]
    public void ProtocolVersion_Parse_Should_ParseValidVersions()
    {
        // Act
        var version = ProtocolVersion.Parse("1.2.3");

        // Assert
        version.Major.Should().Be(1);
        version.Minor.Should().Be(2);
        version.Patch.Should().Be(3);
        version.Version.Should().Be("1.2.3");
    }

    [Theory]
    [InlineData("1.0.0", "1.0.0", true)] // Same version
    [InlineData("1.0.0", "1.0.1", true)] // Compatible patch
    [InlineData("1.1.0", "1.0.0", true)] // Server newer minor
    [InlineData("1.0.0", "1.1.0", false)] // Client newer minor
    [InlineData("1.0.0", "2.0.0", false)] // Different major
    [InlineData("2.0.0", "1.0.0", false)] // Different major
    public void ProtocolVersion_IsCompatibleWith_Should_WorkCorrectly(string version1, string version2, bool expected)
    {
        // Arrange
        var v1 = ProtocolVersion.Parse(version1);
        var v2 = ProtocolVersion.Parse(version2);

        // Act
        var result = v1.IsCompatibleWith(v2);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("1.0.0", "2.0.0", -1)]
    [InlineData("2.0.0", "1.0.0", 1)]
    [InlineData("1.0.0", "1.0.0", 0)]
    [InlineData("1.0.0", "1.1.0", -1)]
    [InlineData("1.1.0", "1.0.0", 1)]
    [InlineData("1.0.0", "1.0.1", -1)]
    [InlineData("1.0.1", "1.0.0", 1)]
    public void ProtocolVersion_CompareTo_Should_WorkCorrectly(string version1, string version2, int expected)
    {
        // Arrange
        var v1 = ProtocolVersion.Parse(version1);
        var v2 = ProtocolVersion.Parse(version2);

        // Act
        var result = v1.CompareTo(v2);

        // Assert
        result.Should().Be(expected);
    }
}