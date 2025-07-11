using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace McpServer.{{LAYER}}.Tests.{{NAMESPACE}};

/// <summary>
/// Unit tests for {{CLASS_NAME}}
/// </summary>
[TestFixture]
public class {{CLASS_NAME}}Tests
{
    private Mock<ILogger<{{CLASS_NAME}}>> _loggerMock;
    private Mock<{{DEPENDENCY_INTERFACE}}> _dependencyMock;
    private {{CLASS_NAME}} _sut; // System Under Test

    [SetUp]
    public void Setup()
    {
        // Arrange common test dependencies
        _loggerMock = new Mock<ILogger<{{CLASS_NAME}}>>();
        _dependencyMock = new Mock<{{DEPENDENCY_INTERFACE}}>();
        
        _sut = new {{CLASS_NAME}}(
            _loggerMock.Object,
            _dependencyMock.Object);
    }

    [TearDown]
    public void TearDown()
    {
        // Clean up any resources if needed
    }

    #region Constructor Tests

    [Test]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new {{CLASS_NAME}}(null!, _dependencyMock.Object));
    }

    [Test]
    public void Constructor_NullDependency_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new {{CLASS_NAME}}(_loggerMock.Object, null!));
    }

    #endregion

    #region {{METHOD_NAME}} Tests

    [Test]
    public async Task {{METHOD_NAME}}_ValidInput_ReturnsExpectedResult()
    {
        // Arrange
        var input = {{TEST_INPUT}};
        var expectedResult = {{EXPECTED_RESULT}};
        
        _dependencyMock
            .Setup(x => x.{{MOCK_METHOD}}(It.IsAny<{{PARAM_TYPE}}>()))
            .ReturnsAsync({{MOCK_RETURN}});

        // Act
        var result = await _sut.{{METHOD_NAME}}Async(input);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.EqualTo(expectedResult));
        
        // Verify interactions
        _dependencyMock.Verify(x => x.{{MOCK_METHOD}}(
            It.Is<{{PARAM_TYPE}}>(p => {{PARAM_ASSERTION}})), 
            Times.Once);
    }

    [Test]
    public async Task {{METHOD_NAME}}_NullInput_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _sut.{{METHOD_NAME}}Async(null!));
        
        Assert.That(ex.ParamName, Is.EqualTo("{{PARAM_NAME}}"));
    }

    [Test]
    public async Task {{METHOD_NAME}}_DependencyThrows_PropagatesException()
    {
        // Arrange
        var input = {{TEST_INPUT}};
        var expectedException = new InvalidOperationException("Dependency failed");
        
        _dependencyMock
            .Setup(x => x.{{MOCK_METHOD}}(It.IsAny<{{PARAM_TYPE}}>()))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _sut.{{METHOD_NAME}}Async(input));
        
        Assert.That(ex, Is.SameAs(expectedException));
    }

    [Test]
    public async Task {{METHOD_NAME}}_Cancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var input = {{TEST_INPUT}};
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        Assert.ThrowsAsync<OperationCanceledException>(
            async () => await _sut.{{METHOD_NAME}}Async(input, cts.Token));
    }

    #endregion

    #region Edge Cases

    [TestCase("")]
    [TestCase(" ")]
    [TestCase(null)]
    public void {{METHOD_NAME}}_InvalidStringInput_ThrowsArgumentException(string? invalidInput)
    {
        // Act & Assert
        var ex = Assert.ThrowsAsync<ArgumentException>(
            async () => await _sut.{{METHOD_NAME}}Async(invalidInput!));
        
        Assert.That(ex.Message, Does.Contain("{{PARAM_NAME}}"));
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(int.MaxValue)]
    public async Task {{METHOD_NAME}}_BoundaryValues_HandledCorrectly(int boundaryValue)
    {
        // Arrange
        var input = {{CREATE_INPUT_WITH_VALUE}};
        
        // Act
        var result = await _sut.{{METHOD_NAME}}Async(input);
        
        // Assert
        Assert.That(result, Is.Not.Null);
        // Add specific assertions for boundary behavior
    }

    #endregion

    #region Performance Tests

    [Test]
    [Timeout(1000)] // 1 second timeout
    public async Task {{METHOD_NAME}}_LargeDataSet_CompletesWithinTimeout()
    {
        // Arrange
        var largeInput = {{CREATE_LARGE_INPUT}};
        
        // Act
        var result = await _sut.{{METHOD_NAME}}Async(largeInput);
        
        // Assert
        Assert.That(result, Is.Not.Null);
    }

    #endregion

    #region Integration Scenarios

    [Test]
    public async Task {{METHOD_NAME}}_MultipleCallsConcurrently_ThreadSafe()
    {
        // Arrange
        var tasks = new Task[10];
        
        // Act
        for (int i = 0; i < tasks.Length; i++)
        {
            var input = {{CREATE_INPUT_WITH_INDEX}};
            tasks[i] = _sut.{{METHOD_NAME}}Async(input);
        }
        
        await Task.WhenAll(tasks);
        
        // Assert
        // Verify no exceptions and correct behavior
        _dependencyMock.Verify(x => x.{{MOCK_METHOD}}(
            It.IsAny<{{PARAM_TYPE}}>()), 
            Times.Exactly(10));
    }

    #endregion

    #region Test Helpers

    private {{INPUT_TYPE}} CreateValidInput()
    {
        return new {{INPUT_TYPE}}
        {
            // Set up valid test data
            {{VALID_INPUT_PROPERTIES}}
        };
    }

    private void AssertResultIsValid({{RESULT_TYPE}} result)
    {
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            // Add more specific assertions
            {{RESULT_ASSERTIONS}}
        });
    }

    #endregion
}

// Test data builder for complex scenarios
public class {{CLASS_NAME}}TestDataBuilder
{
    private {{INPUT_TYPE}} _input = new();

    public {{CLASS_NAME}}TestDataBuilder WithDefaultValues()
    {
        _input = new {{INPUT_TYPE}}
        {
            {{DEFAULT_VALUES}}
        };
        return this;
    }

    public {{CLASS_NAME}}TestDataBuilder With{{PROPERTY_NAME}}({{PROPERTY_TYPE}} value)
    {
        _input.{{PROPERTY_NAME}} = value;
        return this;
    }

    public {{INPUT_TYPE}} Build() => _input;
}