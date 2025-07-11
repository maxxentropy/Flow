using FluentAssertions;
using McpServer.Application.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace McpServer.Application.Tests.Caching;

public class MemoryCacheServiceTests : IDisposable
{
    private readonly Mock<ILogger<MemoryCacheService>> _loggerMock;
    private readonly MemoryCacheService _cacheService;
    private readonly McpServer.Application.Caching.MemoryCacheOptions _options;
    
    public MemoryCacheServiceTests()
    {
        _loggerMock = new Mock<ILogger<MemoryCacheService>>();
        _options = new McpServer.Application.Caching.MemoryCacheOptions
        {
            SizeLimit = 1024 * 1024, // 1MB
            CompactionPercentage = 0.1,
            ExpirationScanFrequency = TimeSpan.FromSeconds(1)
        };
        
        _cacheService = new MemoryCacheService(_loggerMock.Object, Options.Create(_options));
    }
    
    [Fact]
    public void TryGetValue_WithNonExistentKey_ReturnsFalse()
    {
        // Act
        var result = _cacheService.TryGetValue<string>("nonexistent", out var value);
        
        // Assert
        result.Should().BeFalse();
        value.Should().BeNull();
    }
    
    [Fact]
    public void TryGetValue_WithExistingKey_ReturnsTrue()
    {
        // Arrange
        var key = "test-key";
        var expectedValue = "test-value";
        var options = new CacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(5) };
        
        _cacheService.Set(key, expectedValue, options);
        
        // Act
        var result = _cacheService.TryGetValue<string>(key, out var value);
        
        // Assert
        result.Should().BeTrue();
        value.Should().Be(expectedValue);
    }
    
    [Fact]
    public async Task TryGetValueAsync_WithExistingKey_ReturnsValue()
    {
        // Arrange
        var key = "async-key";
        var expectedValue = "async-value";
        var options = new CacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(5) };
        
        await _cacheService.SetAsync(key, expectedValue, options);
        
        // Act
        var (found, value) = await _cacheService.TryGetValueAsync<string>(key);
        
        // Assert
        found.Should().BeTrue();
        value.Should().Be(expectedValue);
    }
    
    [Fact]
    public void Set_WithAbsoluteExpiration_StoresValue()
    {
        // Arrange
        var key = "abs-exp-key";
        var value = "abs-exp-value";
        var expiration = DateTimeOffset.UtcNow.AddMinutes(10);
        
        // Act
        _cacheService.Set(key, value, expiration);
        
        // Assert
        var result = _cacheService.TryGetValue<string>(key, out var retrievedValue);
        result.Should().BeTrue();
        retrievedValue.Should().Be(value);
    }
    
    [Fact]
    public void Set_WithSlidingExpiration_StoresValue()
    {
        // Arrange
        var key = "sliding-exp-key";
        var value = "sliding-exp-value";
        var expiration = TimeSpan.FromMinutes(5);
        
        // Act
        _cacheService.Set(key, value, expiration);
        
        // Assert
        var result = _cacheService.TryGetValue<string>(key, out var retrievedValue);
        result.Should().BeTrue();
        retrievedValue.Should().Be(value);
    }
    
    [Fact]
    public void Set_WithCacheEntryOptions_StoresValue()
    {
        // Arrange
        var key = "options-key";
        var value = "options-value";
        var options = new CacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(5),
            Priority = CachePriority.High,
            Size = 100
        };
        
        // Act
        _cacheService.Set(key, value, options);
        
        // Assert
        var result = _cacheService.TryGetValue<string>(key, out var retrievedValue);
        result.Should().BeTrue();
        retrievedValue.Should().Be(value);
    }
    
    [Fact]
    public void GetOrCreate_WithNonExistentKey_CreatesAndReturnsValue()
    {
        // Arrange
        var key = "create-key";
        var expectedValue = "created-value";
        var options = new CacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(5) };
        
        // Act
        var result = _cacheService.GetOrCreate(key, () => expectedValue, options);
        
        // Assert
        result.Should().Be(expectedValue);
        
        // Verify it was cached
        var cached = _cacheService.TryGetValue<string>(key, out var cachedValue);
        cached.Should().BeTrue();
        cachedValue.Should().Be(expectedValue);
    }
    
    [Fact]
    public void GetOrCreate_WithExistingKey_ReturnsExistingValue()
    {
        // Arrange
        var key = "existing-key";
        var existingValue = "existing-value";
        var newValue = "new-value";
        var options = new CacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(5) };
        
        _cacheService.Set(key, existingValue, options);
        
        // Act
        var result = _cacheService.GetOrCreate(key, () => newValue, options);
        
        // Assert
        result.Should().Be(existingValue);
    }
    
    [Fact]
    public async Task GetOrCreateAsync_WithNonExistentKey_CreatesAndReturnsValue()
    {
        // Arrange
        var key = "async-create-key";
        var expectedValue = "async-created-value";
        var options = new CacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(5) };
        
        // Act
        var result = await _cacheService.GetOrCreateAsync(key, async () =>
        {
            await Task.Delay(10); // Simulate async work
            return expectedValue;
        }, options);
        
        // Assert
        result.Should().Be(expectedValue);
        
        // Verify it was cached
        var cached = _cacheService.TryGetValue<string>(key, out var cachedValue);
        cached.Should().BeTrue();
        cachedValue.Should().Be(expectedValue);
    }
    
    [Fact]
    public void Remove_WithExistingKey_RemovesValue()
    {
        // Arrange
        var key = "remove-key";
        var value = "remove-value";
        var options = new CacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(5) };
        
        _cacheService.Set(key, value, options);
        
        // Act
        var removed = _cacheService.Remove(key);
        
        // Assert
        removed.Should().BeTrue();
        
        var exists = _cacheService.TryGetValue<string>(key, out _);
        exists.Should().BeFalse();
    }
    
    [Fact]
    public void RemoveByPattern_WithMatchingKeys_RemovesMatchingEntries()
    {
        // Arrange
        var options = new CacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(5) };
        _cacheService.Set("test:key1", "value1", options);
        _cacheService.Set("test:key2", "value2", options);
        _cacheService.Set("other:key3", "value3", options);
        
        // Act
        var removedCount = _cacheService.RemoveByPattern("test:*");
        
        // Assert
        removedCount.Should().Be(2);
        
        _cacheService.TryGetValue<string>("test:key1", out _).Should().BeFalse();
        _cacheService.TryGetValue<string>("test:key2", out _).Should().BeFalse();
        _cacheService.TryGetValue<string>("other:key3", out _).Should().BeTrue();
    }
    
    [Fact]
    public void Clear_RemovesAllEntries()
    {
        // Arrange
        var options = new CacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(5) };
        _cacheService.Set("key1", "value1", options);
        _cacheService.Set("key2", "value2", options);
        
        // Act
        _cacheService.Clear();
        
        // Assert
        _cacheService.TryGetValue<string>("key1", out _).Should().BeFalse();
        _cacheService.TryGetValue<string>("key2", out _).Should().BeFalse();
    }
    
    [Fact]
    public void GetStatistics_ReturnsCorrectStatistics()
    {
        // Arrange
        var options = new CacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(5) };
        _cacheService.Set("key1", "value1", options);
        _cacheService.Set("key2", "value2", options);
        
        // Generate some hits and misses
        _cacheService.TryGetValue<string>("key1", out _); // Hit
        _cacheService.TryGetValue<string>("key2", out _); // Hit
        _cacheService.TryGetValue<string>("nonexistent", out _); // Miss
        
        // Act
        var stats = _cacheService.GetStatistics();
        
        // Assert
        stats.EntryCount.Should().Be(2);
        stats.HitCount.Should().Be(2);
        stats.MissCount.Should().Be(1);
        stats.HitRatio.Should().BeApproximately(0.67, 0.01);
    }
    
    [Fact]
    public void Set_WithEvictionCallback_CallsCallbackOnEviction()
    {
        // Arrange
        var key = "eviction-key";
        var value = "eviction-value";
        var callbackInvoked = false;
        string? evictedKey = null;
        object? evictedValue = null;
        McpServer.Application.Caching.EvictionReason? evictionReason = null;
        
        var options = new CacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(5),
            Callbacks = new CacheEntryCallbacks
            {
                OnEviction = (k, v, r) =>
                {
                    callbackInvoked = true;
                    evictedKey = k;
                    evictedValue = v;
                    evictionReason = r;
                }
            }
        };
        
        _cacheService.Set(key, value, options);
        
        // Act
        _cacheService.Remove(key);
        
        // Wait a bit for async eviction callback
        Thread.Sleep(100);
        
        // Assert
        callbackInvoked.Should().BeTrue();
        evictedKey.Should().Be(key);
        evictedValue.Should().Be(value);
        evictionReason.Should().Be(McpServer.Application.Caching.EvictionReason.Removed);
    }
    
    [Fact]
    public void Set_WithNullKey_ThrowsArgumentNullException()
    {
        // Arrange
        var options = new CacheEntryOptions();
        
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _cacheService.Set<string>(null!, "value", options));
    }
    
    [Fact]
    public void Set_WithNullValue_ThrowsArgumentNullException()
    {
        // Arrange
        var options = new CacheEntryOptions();
        
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _cacheService.Set<object>("key", null!, options));
    }
    
    [Fact]
    public async Task Concurrent_Access_IsThreadSafe()
    {
        // Arrange
        var tasks = new List<Task>();
        var options = new CacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(5) };
        
        // Act - Multiple threads accessing cache concurrently
        for (int i = 0; i < 10; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 100; j++)
                {
                    var key = $"key-{index}-{j}";
                    var value = $"value-{index}-{j}";
                    
                    _cacheService.Set(key, value, options);
                    _cacheService.TryGetValue<string>(key, out _);
                }
            }));
        }
        
        await Task.WhenAll(tasks);
        
        // Assert - No exceptions should be thrown
        var stats = _cacheService.GetStatistics();
        stats.EntryCount.Should().Be(1000);
        stats.HitCount.Should().Be(1000);
    }
    
    public void Dispose()
    {
        _cacheService?.Dispose();
    }
}