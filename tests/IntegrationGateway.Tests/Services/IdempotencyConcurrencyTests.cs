using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using IntegrationGateway.Services.Implementation;
using Xunit;
using Xunit.Abstractions;

namespace IntegrationGateway.Tests.Services;

/// <summary>
/// Enterprise-grade idempotency tests
/// Tests the core locking and concurrency logic for high-load scenarios
/// </summary>
public class IdempotencyConcurrencyTests
{
    private readonly ITestOutputHelper _output;

    public IdempotencyConcurrencyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task GetOrCreateOperationAsync_HighConcurrency_OnlyOneNewOperation()
    {
        // Arrange
        var serviceProvider = new ServiceCollection()
            .AddLogging(builder => builder.AddConsole())
            .BuildServiceProvider();
        
        var logger = serviceProvider.GetRequiredService<ILogger<IdempotencyService>>();
        var idempotencyService = new IdempotencyService(logger);
        
        const int concurrentRequests = 100;
        const string idempotencyKey = "test-concurrent-key";
        const string operation = "POST_/api/v1/products";
        const string bodyHash = "same-body-hash-for-all";
        
        var results = new ConcurrentBag<bool>(); // Track IsExisting results
        var stopwatch = Stopwatch.StartNew();
        
        // Act - 100 concurrent requests with same idempotency key
        var tasks = Enumerable.Range(0, concurrentRequests)
            .Select(async i =>
            {
                try
                {
                    var (isExisting, _) = await idempotencyService.GetOrCreateOperationAsync(
                        idempotencyKey, operation, bodyHash);
                    
                    results.Add(isExisting);
                    
                    // Simulate processing time
                    await Task.Delay(Random.Shared.Next(1, 5));
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"Task {i} failed: {ex.Message}");
                    throw;
                }
            });
        
        await Task.WhenAll(tasks);
        stopwatch.Stop();
        
        // Assert
        var allResults = results.ToList();
        var newOperations = allResults.Count(r => !r); // IsExisting = false
        var existingOperations = allResults.Count(r => r); // IsExisting = true
        
        _output.WriteLine($"Completed {allResults.Count} operations in {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"New operations: {newOperations}");
        _output.WriteLine($"Existing operations: {existingOperations}");
        _output.WriteLine($"Average time per operation: {stopwatch.ElapsedMilliseconds / (double)concurrentRequests:F2}ms");
        
        // Exactly one should be new, rest should be existing
        Assert.Equal(1, newOperations);
        Assert.Equal(concurrentRequests - 1, existingOperations);
        Assert.Equal(concurrentRequests, allResults.Count);
    }

    [Fact]
    public async Task GetOrCreateOperationAsync_DifferentKeys_AllNewOperations()
    {
        // Arrange
        var serviceProvider = new ServiceCollection()
            .AddLogging(builder => builder.AddConsole())
            .BuildServiceProvider();
        
        var logger = serviceProvider.GetRequiredService<ILogger<IdempotencyService>>();
        var idempotencyService = new IdempotencyService(logger);
        
        const int concurrentRequests = 50;
        const string operation = "POST_/api/v1/products";
        
        var results = new ConcurrentBag<(string Key, bool IsExisting)>();
        var stopwatch = Stopwatch.StartNew();
        
        // Act - Concurrent requests with different keys
        var tasks = Enumerable.Range(0, concurrentRequests)
            .Select(async i =>
            {
                var uniqueKey = $"unique-key-{i}";
                var bodyHash = $"hash-{i}";
                
                var (isExisting, _) = await idempotencyService.GetOrCreateOperationAsync(
                    uniqueKey, operation, bodyHash);
                
                results.Add((uniqueKey, isExisting));
                
                await Task.Delay(Random.Shared.Next(1, 3));
            });
        
        await Task.WhenAll(tasks);
        stopwatch.Stop();
        
        // Assert
        var allResults = results.ToList();
        _output.WriteLine($"Processed {allResults.Count} unique operations in {stopwatch.ElapsedMilliseconds}ms");
        
        // All should be new operations since keys are different
        Assert.All(allResults, result => Assert.False(result.IsExisting));
        Assert.Equal(concurrentRequests, allResults.Count);
        
        _output.WriteLine($"All {allResults.Count} operations were new (expected for different keys)");
    }

    [Fact]
    public async Task GetOrCreateOperationAsync_MixedScenario_CorrectDistribution()
    {
        // Arrange
        var serviceProvider = new ServiceCollection()
            .AddLogging(builder => builder.AddConsole())
            .BuildServiceProvider();
        
        var logger = serviceProvider.GetRequiredService<ILogger<IdempotencyService>>();
        var idempotencyService = new IdempotencyService(logger);
        
        const int duplicateGroups = 3;
        const int requestsPerGroup = 15;
        const int uniqueRequests = 20;
        const string operation = "PUT_/api/v1/products/123";
        
        var results = new ConcurrentBag<(string Key, bool IsExisting, string GroupType)>();
        var tasks = new List<Task>();
        
        // Add duplicate groups
        for (int group = 0; group < duplicateGroups; group++)
        {
            var groupKey = $"group-{group}-key";
            var bodyHash = $"body-hash-group-{group}";
            
            for (int i = 0; i < requestsPerGroup; i++)
            {
                var groupId = group; // Capture for closure
                tasks.Add(Task.Run(async () =>
                {
                    var (isExisting, _) = await idempotencyService.GetOrCreateOperationAsync(
                        groupKey, operation, bodyHash);
                    
                    results.Add((groupKey, isExisting, $"duplicate-group-{groupId}"));
                    
                    await Task.Delay(Random.Shared.Next(1, 5));
                }));
            }
        }
        
        // Add unique requests
        for (int i = 0; i < uniqueRequests; i++)
        {
            var uniqueId = i; // Capture for closure
            tasks.Add(Task.Run(async () =>
            {
                var uniqueKey = $"unique-{uniqueId}";
                var bodyHash = $"unique-body-{uniqueId}";
                
                var (isExisting, _) = await idempotencyService.GetOrCreateOperationAsync(
                    uniqueKey, operation, bodyHash);
                
                results.Add((uniqueKey, isExisting, "unique"));
            }));
        }
        
        // Shuffle tasks
        var random = new Random();
        for (int i = tasks.Count - 1; i > 0; i--)
        {
            int j = random.Next(0, i + 1);
            (tasks[i], tasks[j]) = (tasks[j], tasks[i]);
        }
        
        var stopwatch = Stopwatch.StartNew();
        
        // Act
        await Task.WhenAll(tasks);
        stopwatch.Stop();
        
        // Assert
        var allResults = results.ToList();
        _output.WriteLine($"Processed {allResults.Count} mixed operations in {stopwatch.ElapsedMilliseconds}ms");
        
        // Analyze duplicate groups
        for (int group = 0; group < duplicateGroups; group++)
        {
            var groupResults = allResults.Where(r => r.GroupType == $"duplicate-group-{group}").ToList();
            var newInGroup = groupResults.Count(r => !r.IsExisting);
            var existingInGroup = groupResults.Count(r => r.IsExisting);
            
            _output.WriteLine($"Group {group}: {newInGroup} new, {existingInGroup} existing");
            
            Assert.Equal(1, newInGroup); // Exactly 1 new per group
            Assert.Equal(requestsPerGroup - 1, existingInGroup); // Rest existing
        }
        
        // Analyze unique requests
        var uniqueResults = allResults.Where(r => r.GroupType == "unique").ToList();
        Assert.All(uniqueResults, r => Assert.False(r.IsExisting)); // All unique should be new
        Assert.Equal(uniqueRequests, uniqueResults.Count);
        
        _output.WriteLine($"Unique requests: all {uniqueResults.Count} were new (as expected)");
    }

    [Fact]
    public async Task GetOrCreateOperationAsync_PerformanceBenchmark()
    {
        // Arrange
        var serviceProvider = new ServiceCollection()
            .AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning))
            .BuildServiceProvider();
        
        var logger = serviceProvider.GetRequiredService<ILogger<IdempotencyService>>();
        var idempotencyService = new IdempotencyService(logger);
        
        const int totalOperations = 1000;
        const string operation = "POST_/api/v1/products";
        
        var operationTimes = new ConcurrentBag<TimeSpan>();
        var stopwatch = Stopwatch.StartNew();
        
        // Act - Run many operations concurrently
        var tasks = Enumerable.Range(0, totalOperations)
            .Select(async i =>
            {
                var operationStopwatch = Stopwatch.StartNew();
                
                var key = $"perf-test-{i}";
                var bodyHash = $"hash-{i}";
                
                var (_, _) = await idempotencyService.GetOrCreateOperationAsync(
                    key, operation, bodyHash);
                
                operationStopwatch.Stop();
                operationTimes.Add(operationStopwatch.Elapsed);
            });
        
        await Task.WhenAll(tasks);
        stopwatch.Stop();
        
        // Assert and Report
        var allTimes = operationTimes.ToList();
        var avgTime = allTimes.Average(ts => ts.TotalMilliseconds);
        var minTime = allTimes.Min(ts => ts.TotalMilliseconds);
        var maxTime = allTimes.Max(ts => ts.TotalMilliseconds);
        var throughput = totalOperations / stopwatch.Elapsed.TotalSeconds;
        
        _output.WriteLine($"\n=== Performance Benchmark Results ===");
        _output.WriteLine($"Total Operations: {totalOperations}");
        _output.WriteLine($"Total Time: {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Average Time per Operation: {avgTime:F2}ms");
        _output.WriteLine($"Min Time: {minTime:F2}ms");
        _output.WriteLine($"Max Time: {maxTime:F2}ms");
        _output.WriteLine($"Throughput: {throughput:F0} operations/second");
        
        // Performance assertions (relaxed for different environments)
        Assert.True(avgTime < 100, $"Average operation time should be < 100ms, actual: {avgTime:F2}ms");
        Assert.True(throughput > 50, $"Throughput should be > 50 ops/sec, actual: {throughput:F0}");
        
        _output.WriteLine("✅ Performance benchmarks passed!");
    }
}