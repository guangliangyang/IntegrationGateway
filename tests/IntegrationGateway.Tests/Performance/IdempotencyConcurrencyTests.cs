using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using IntegrationGateway.Models.DTOs;
using IntegrationGateway.Services.Implementation;
using IntegrationGateway.Services.Interfaces;
using Xunit;
using Xunit.Abstractions;

namespace IntegrationGateway.Tests.Performance;

public class IdempotencyConcurrencyTests
{
    private readonly ITestOutputHelper _output;
    private readonly IIdempotencyService _idempotencyService;

    public IdempotencyConcurrencyTests(ITestOutputHelper output)
    {
        _output = output;
        
        var serviceProvider = new ServiceCollection()
            .AddLogging(builder => builder.AddConsole())
            .BuildServiceProvider();
        
        var logger = serviceProvider.GetRequiredService<ILogger<IdempotencyService>>();
        _idempotencyService = new IdempotencyService(logger);
    }

    [Fact]
    public async Task IdempotencyService_HighConcurrency_ShouldHandleCorrectly()
    {
        // Arrange
        const int concurrentRequests = 100;
        const string idempotencyKey = "test-concurrent-key";
        const string operation = "POST_/api/v1/products";
        const string bodyHash = "same-body-hash";
        
        var results = new ConcurrentBag<(bool IsExisting, string OperationId)>();
        var stopwatch = Stopwatch.StartNew();
        
        // Act - Simulate 100 concurrent requests with same idempotency key
        var tasks = Enumerable.Range(0, concurrentRequests)
            .Select(async i =>
            {
                try
                {
                    var (isExisting, operation) = await _idempotencyService.GetOrCreateOperationAsync(
                        idempotencyKey, operation, bodyHash);
                    
                    results.Add((isExisting, operation.GetCompositeKey()));
                    
                    // Simulate some processing time
                    await Task.Delay(Random.Shared.Next(1, 10));
                    
                    // Update with response (simulate different responses)
                    await _idempotencyService.UpdateOperationResponseAsync(
                        idempotencyKey, operation, bodyHash, 
                        $"{{\"id\":\"prod-{i}\"}}", 201);
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
        _output.WriteLine($"Completed {allResults.Count} operations in {stopwatch.ElapsedMilliseconds}ms");
        
        // Exactly one should be new (IsExisting = false), rest should be existing (IsExisting = true)
        var newOperations = allResults.Where(r => !r.IsExisting).ToList();
        var existingOperations = allResults.Where(r => r.IsExisting).ToList();
        
        Assert.Single(newOperations); // Only one new operation
        Assert.Equal(concurrentRequests - 1, existingOperations.Count); // Rest are duplicates
        
        _output.WriteLine($"New operations: {newOperations.Count}");
        _output.WriteLine($"Existing operations: {existingOperations.Count}");
        _output.WriteLine($"Average time per operation: {stopwatch.ElapsedMilliseconds / (double)concurrentRequests:F2}ms");
    }

    [Fact]
    public async Task IdempotencyService_DifferentKeys_ShouldAllowConcurrentProcessing()
    {
        // Arrange
        const int concurrentRequests = 50;
        const string operation = "POST_/api/v1/products";
        const string bodyHash = "different-body-hash";
        
        var results = new ConcurrentBag<(string Key, bool IsExisting)>();
        var stopwatch = Stopwatch.StartNew();
        
        // Act - Simulate concurrent requests with different idempotency keys
        var tasks = Enumerable.Range(0, concurrentRequests)
            .Select(async i =>
            {
                var uniqueKey = $"unique-key-{i}";
                var (isExisting, _) = await _idempotencyService.GetOrCreateOperationAsync(
                    uniqueKey, operation, bodyHash);
                
                results.Add((uniqueKey, isExisting));
                
                // Simulate processing
                await Task.Delay(Random.Shared.Next(1, 5));
                
                await _idempotencyService.UpdateOperationResponseAsync(
                    uniqueKey, operation, bodyHash, 
                    $"{{\"id\":\"prod-{i}\"}}", 201);
            });
        
        await Task.WhenAll(tasks);
        stopwatch.Stop();
        
        // Assert
        var allResults = results.ToList();
        _output.WriteLine($"Processed {allResults.Count} unique operations in {stopwatch.ElapsedMilliseconds}ms");
        
        // All should be new operations (IsExisting = false) since they have different keys
        Assert.All(allResults, result => Assert.False(result.IsExisting));
        Assert.Equal(concurrentRequests, allResults.Count);
        
        _output.WriteLine($"All {allResults.Count} operations were processed as new (expected for different keys)");
    }

    [Fact] 
    public async Task IdempotencyService_MixedScenario_ShouldHandleCorrectly()
    {
        // Arrange - Mix of duplicate and unique requests
        const int duplicateGroups = 5;
        const int requestsPerGroup = 20;
        const int uniqueRequests = 30;
        const string operation = "PUT_/api/v1/products/123";
        
        var results = new ConcurrentBag<(string Key, bool IsExisting, string GroupType)>();
        var stopwatch = Stopwatch.StartNew();
        
        var tasks = new List<Task>();
        
        // Add duplicate groups (same idempotency key per group)
        for (int group = 0; group < duplicateGroups; group++)
        {
            var groupKey = $"group-{group}-key";
            var bodyHash = $"body-hash-group-{group}";
            
            for (int i = 0; i < requestsPerGroup; i++)
            {
                tasks.Add(ProcessRequest(groupKey, bodyHash, $"duplicate-group-{group}"));
            }
        }
        
        // Add unique requests
        for (int i = 0; i < uniqueRequests; i++)
        {
            var uniqueKey = $"unique-{i}";
            var bodyHash = $"unique-body-{i}";
            tasks.Add(ProcessRequest(uniqueKey, bodyHash, "unique"));
        }
        
        // Shuffle tasks to simulate random arrival
        var random = new Random();
        for (int i = tasks.Count - 1; i > 0; i--)
        {
            int j = random.Next(0, i + 1);
            (tasks[i], tasks[j]) = (tasks[j], tasks[i]);
        }
        
        // Act
        await Task.WhenAll(tasks);
        stopwatch.Stop();
        
        // Assert
        var allResults = results.ToList();
        _output.WriteLine($"Processed {allResults.Count} mixed operations in {stopwatch.ElapsedMilliseconds}ms");
        
        // Group analysis
        var duplicateResults = allResults.Where(r => r.GroupType.StartsWith("duplicate-group")).ToList();
        var uniqueResults = allResults.Where(r => r.GroupType == "unique").ToList();
        
        // Each duplicate group should have exactly 1 new operation, rest existing
        for (int group = 0; group < duplicateGroups; group++)
        {
            var groupResults = duplicateResults.Where(r => r.GroupType == $"duplicate-group-{group}").ToList();
            var newInGroup = groupResults.Count(r => !r.IsExisting);
            var existingInGroup = groupResults.Count(r => r.IsExisting);
            
            Assert.Equal(1, newInGroup);
            Assert.Equal(requestsPerGroup - 1, existingInGroup);
            
            _output.WriteLine($"Group {group}: {newInGroup} new, {existingInGroup} existing");
        }
        
        // All unique requests should be new
        Assert.All(uniqueResults, r => Assert.False(r.IsExisting));
        _output.WriteLine($"Unique requests: all {uniqueResults.Count} were new (as expected)");
        
        async Task ProcessRequest(string key, string bodyHash, string groupType)
        {
            var (isExisting, _) = await _idempotencyService.GetOrCreateOperationAsync(
                key, operation, bodyHash);
            
            results.Add((key, isExisting, groupType));
            
            await Task.Delay(Random.Shared.Next(1, 5));
            
            await _idempotencyService.UpdateOperationResponseAsync(
                key, operation, bodyHash, "{\"status\":\"updated\"}", 200);
        }
    }

    [Fact]
    public async Task IdempotencyService_PerformanceBenchmark()
    {
        // Arrange
        const int totalOperations = 1000;
        const int concurrentGroups = 10;
        const int operationsPerGroup = totalOperations / concurrentGroups;
        
        var stopwatch = Stopwatch.StartNew();
        var results = new ConcurrentBag<TimeSpan>();
        
        // Act - Run operations in concurrent groups
        var groupTasks = Enumerable.Range(0, concurrentGroups)
            .Select(async groupId =>
            {
                var groupStopwatch = Stopwatch.StartNew();
                
                var tasks = Enumerable.Range(0, operationsPerGroup)
                    .Select(async i =>
                    {
                        var operationStopwatch = Stopwatch.StartNew();
                        
                        var key = $"perf-test-{groupId}-{i}";
                        var operation = "POST_/api/v1/products";
                        var bodyHash = $"hash-{groupId}-{i}";
                        
                        var (isExisting, _) = await _idempotencyService.GetOrCreateOperationAsync(
                            key, operation, bodyHash);
                        
                        await _idempotencyService.UpdateOperationResponseAsync(
                            key, operation, bodyHash, "{\"id\":\"test\"}", 201);
                        
                        operationStopwatch.Stop();
                        results.Add(operationStopwatch.Elapsed);
                    });
                
                await Task.WhenAll(tasks);
                groupStopwatch.Stop();
                
                _output.WriteLine($"Group {groupId} completed {operationsPerGroup} operations in {groupStopwatch.ElapsedMilliseconds}ms");
            });
        
        await Task.WhenAll(groupTasks);
        stopwatch.Stop();
        
        // Assert and Report
        var allResults = results.ToList();
        var avgTime = allResults.Average(ts => ts.TotalMilliseconds);
        var minTime = allResults.Min(ts => ts.TotalMilliseconds);
        var maxTime = allResults.Max(ts => ts.TotalMilliseconds);
        var totalThroughput = totalOperations / stopwatch.Elapsed.TotalSeconds;
        
        _output.WriteLine($"\n=== Performance Benchmark Results ===");
        _output.WriteLine($"Total Operations: {totalOperations}");
        _output.WriteLine($"Total Time: {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Average Time per Operation: {avgTime:F2}ms");
        _output.WriteLine($"Min Time: {minTime:F2}ms");
        _output.WriteLine($"Max Time: {maxTime:F2}ms");
        _output.WriteLine($"Throughput: {totalThroughput:F0} operations/second");
        
        // Performance assertions
        Assert.True(avgTime < 50, $"Average operation time should be < 50ms, actual: {avgTime:F2}ms");
        Assert.True(totalThroughput > 100, $"Throughput should be > 100 ops/sec, actual: {totalThroughput:F0}");
    }
}