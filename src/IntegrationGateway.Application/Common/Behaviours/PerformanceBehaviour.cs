using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntegrationGateway.Application.Common.Behaviours;

/// <summary>
/// MediatR pipeline behavior for monitoring and logging performance metrics
/// </summary>
public class PerformanceBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<PerformanceBehaviour<TRequest, TResponse>> _logger;
    private readonly PerformanceOptions _options;
    private readonly Stopwatch _timer;

    public PerformanceBehaviour(ILogger<PerformanceBehaviour<TRequest, TResponse>> logger, 
        IOptions<PerformanceOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        _timer = new Stopwatch();
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        _timer.Start();

        var response = await next();

        _timer.Stop();

        var elapsedMilliseconds = _timer.ElapsedMilliseconds;
        var requestName = typeof(TRequest).Name;

        // Always log performance metrics
        _logger.LogInformation("Performance: {RequestName} completed in {ElapsedMs}ms", 
            requestName, elapsedMilliseconds);

        // Log warning for slow requests
        if (elapsedMilliseconds > _options.SlowRequestThresholdMs)
        {
            _logger.LogWarning("Slow request detected: {RequestName} took {ElapsedMs}ms (threshold: {ThresholdMs}ms)", 
                requestName, elapsedMilliseconds, _options.SlowRequestThresholdMs);
        }

        // Log critical for very slow requests
        if (elapsedMilliseconds > _options.CriticalRequestThresholdMs)
        {
            _logger.LogCritical("Critical slow request: {RequestName} took {ElapsedMs}ms (critical threshold: {CriticalThresholdMs}ms) - Request: {@Request}", 
                requestName, elapsedMilliseconds, _options.CriticalRequestThresholdMs, request);
        }

        return response;
    }
}

/// <summary>
/// Configuration options for performance monitoring
/// </summary>
public class PerformanceOptions
{
    public const string SectionName = "Performance";

    /// <summary>
    /// Threshold in milliseconds for logging slow request warnings
    /// </summary>
    public long SlowRequestThresholdMs { get; set; } = 500;

    /// <summary>
    /// Threshold in milliseconds for logging critical slow requests
    /// </summary>
    public long CriticalRequestThresholdMs { get; set; } = 2000;

    /// <summary>
    /// Enable detailed performance metrics collection
    /// </summary>
    public bool EnableDetailedMetrics { get; set; } = true;

    /// <summary>
    /// Enable memory usage monitoring
    /// </summary>
    public bool EnableMemoryMonitoring { get; set; } = false;
}