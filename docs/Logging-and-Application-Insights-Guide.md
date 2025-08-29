# Logging and Application Insights Guide

## Overview

This guide explains the logging architecture, Application Insights integration, and debugging workflow for the Integration Gateway. It covers what gets tracked, how to configure telemetry, and how to debug issues using Azure Application Insights.

## Logging Architecture

### Core Logging Framework

The project uses **ASP.NET Core ILogger** with structured logging throughout:

```csharp
// Standard logging pattern used across the application
private readonly ILogger<ServiceName> _logger;

_logger.LogInformation("Operation completed: {OperationName} in {ElapsedMs}ms", 
    operationName, elapsedTime);
```

### MediatR Pipeline Behaviors

#### 1. PerformanceBehaviour

**Purpose**: Monitors and logs performance metrics for all MediatR requests

**Implementation Details**:
- Uses `System.Diagnostics.Stopwatch` for precise timing
- Configurable performance thresholds:
  - **SlowRequestThresholdMs**: 500ms (default) - triggers Warning logs
  - **CriticalRequestThresholdMs**: 2000ms (default) - triggers Critical logs

**Logging Levels**:
```csharp
// Information level - all requests
_logger.LogInformation("Performance: {RequestName} completed in {ElapsedMs}ms", 
    requestName, elapsedMilliseconds);

// Warning level - slow requests
_logger.LogWarning("Slow request detected: {RequestName} took {ElapsedMs}ms (threshold: {ThresholdMs}ms)", 
    requestName, elapsedMilliseconds, _options.SlowRequestThresholdMs);

// Critical level - very slow requests  
_logger.LogCritical("Critical slow request: {RequestName} took {ElapsedMs}ms - Request: {@Request}", 
    requestName, elapsedMilliseconds, request);
```

**Configuration**:
```json
{
  "Performance": {
    "SlowRequestThresholdMs": 500,
    "CriticalRequestThresholdMs": 2000,
    "EnableDetailedMetrics": true,
    "EnableMemoryMonitoring": false
  }
}
```

#### 2. LoggingBehaviour

**Purpose**: Structured logging of requests and responses with security filtering

**Key Features**:
- **Request Correlation**: Generates unique GUID for each request
- **Sensitive Data Filtering**: Automatically excludes sensitive properties
- **Safe Logging**: Truncates long strings, handles exceptions gracefully

**Security Filtering**:
```csharp
// These properties are automatically excluded from logs
private static readonly string[] SensitiveKeywords = 
{
    "password", "pwd", "secret", "key", "token", "auth", "credential",
    "ssn", "social", "credit", "card", "cvv", "pin"
};
```

**Log Format**:
```csharp
// Request start
_logger.LogInformation("Handling request: {RequestName} ({RequestId})", 
    requestName, requestGuid);

// Request completion
_logger.LogInformation("Completed request: {RequestName} ({RequestId})", 
    requestName, requestGuid);

// Request failure
_logger.LogError("Request failed: {RequestName} ({RequestId}) - {ExceptionType}: {ExceptionMessage}", 
    requestName, requestGuid, ex.GetType().Name, ex.Message);
```

## Application Insights Configuration

### Connection Setup

**Modern Approach**: Uses connection string (recommended over instrumentation key)

```json
{
  "ApplicationInsights": {
    "ConnectionString": "InstrumentationKey=your-key;IngestionEndpoint=https://...",
    "EnableAdaptiveSampling": true,
    "SamplingPercentage": 100.0,
    "EnableHeartbeat": true,
    "EnableQuickPulseMetricStream": true,
    "EnableDependencyTracking": true,
    "EnablePerformanceCounterCollection": true,
    "CloudRoleName": "IntegrationGateway",
    "CustomProperties": {
      "Environment": "Production",
      "Service": "IntegrationGateway",
      "Version": "1.0.0"
    }
  }
}
```

### Telemetry Initialization

**CloudRoleNameInitializer**: Sets service name in distributed tracing
**CustomPropertiesInitializer**: Adds custom tags to all telemetry

```csharp
public void Initialize(ITelemetry telemetry)
{
    telemetry.Context.Cloud.RoleName = "IntegrationGateway";
    telemetry.Context.GlobalProperties["Environment"] = "Production";
    telemetry.Context.GlobalProperties["Service"] = "IntegrationGateway";
}
```

## What Gets Tracked and Recorded

### 1. HTTP Requests

**Automatic Tracking**:
- Request URL, method, status code
- Response time and payload size
- User agent and client IP
- Custom headers (non-sensitive)

**Custom Properties Added**:
- Request correlation ID
- User context (if authenticated)
- API version (v1/v2)

### 2. Dependencies

**External Service Calls**:
- HTTP calls to ERP Service
- HTTP calls to Warehouse Service
- Response times and success rates
- Failure reasons and retry attempts

**Database Operations**:
- Query execution times
- Connection pooling metrics
- Transaction scope tracking

### 3. Performance Metrics

**MediatR Pipeline**:
- Command/Query execution times
- Validation duration
- Caching hit/miss rates
- Pipeline behavior execution

**Custom Metrics**:
```csharp
// Tracked automatically by PerformanceBehaviour
"Performance: GetProductsV1Query completed in 245ms"
"Slow request detected: CreateProductCommand took 750ms (threshold: 500ms)"
```

### 4. Exceptions and Errors

**Comprehensive Error Tracking**:
- Full exception stack traces
- Request context during failure
- User actions leading to error
- Environment information

**Error Categories**:
- Validation errors (400-level)
- Authentication/Authorization failures (401/403)
- External service failures (upstream dependencies)
- System errors (500-level)

### 5. Business Events

**Product Operations**:
- Product creation/update/deletion
- Cache invalidation events
- Idempotency key usage
- Rate limiting triggers

**Integration Events**:
- ERP service interactions
- Warehouse service synchronization
- Circuit breaker state changes
- Retry policy executions

### 6. Security Events

**Authentication/Authorization**:
- JWT token validation
- Role-based access attempts
- API key usage

**Security Protection**:
- SSRF protection triggers
- Input validation failures
- Rate limiting violations

## Debugging Workflow: Code to Azure Portal

### Step 1: Local Development

**Enable Application Insights Locally**:

1. Create `.env` file in project root:
```bash
APPLICATIONINSIGHTS_CONNECTION_STRING="InstrumentationKey=your-key;IngestionEndpoint=https://..."
```

2. Run the application:
```bash
dotnet run --project src/IntegrationGateway.Api
```

3. Generate some requests:
```bash
curl -X GET "https://localhost:7000/api/v1/products"
curl -X POST "https://localhost:7000/api/v1/products" -H "Content-Type: application/json" -d '{"name":"Test Product"}'
```

### Step 2: Azure Portal Investigation

#### A. Application Map
**Navigate to**: Application Insights > Application Map

**What to Look For**:
- Service dependencies and call flows
- Failure rates between services
- Performance bottlenecks
- Unhealthy dependencies

#### B. Live Metrics Stream
**Navigate to**: Application Insights > Live Metrics

**Real-time Monitoring**:
- Incoming request rates
- Outgoing dependency calls
- Performance counters
- Failures and exceptions

#### C. Failures Analysis
**Navigate to**: Application Insights > Failures

**Key Metrics**:
- Top failing operations
- Exception types and frequencies
- Impact analysis
- End-to-end transaction details

#### D. Performance Analysis
**Navigate to**: Application Insights > Performance

**Performance Insights**:
- Slowest operations identification
- Performance trends over time
- Dependency call duration
- Database query performance

### Step 3: Query Application Insights Data

#### Kusto Queries for Common Scenarios

**1. Performance Analysis**:
```kusto
// Find slow requests in the last 24 hours
requests
| where timestamp > ago(24h)
| where duration > 1000  // requests slower than 1 second
| project timestamp, name, url, duration, resultCode
| order by duration desc
```

**2. Error Investigation**:
```kusto
// Find errors with their exceptions
requests
| where timestamp > ago(24h)
| where success == false
| join (exceptions | project operation_Id, type, outerMessage, details) on operation_Id
| project timestamp, name, resultCode, type, outerMessage
| order by timestamp desc
```

**3. Dependency Health Check**:
```kusto
// Monitor external service health
dependencies
| where timestamp > ago(24h)
| where type == "Http"
| summarize 
    Total = count(),
    Failures = countif(success == false),
    AvgDuration = avg(duration)
  by name
| extend FailureRate = Failures * 100.0 / Total
| order by FailureRate desc
```

**4. Custom Performance Tracking**:
```kusto
// Find performance behavior logs
traces
| where timestamp > ago(24h)
| where message contains "Performance:"
| extend RequestName = extract(@"Performance: (\w+) completed", 1, message)
| extend Duration = extract(@"completed in (\d+)ms", 1, message)
| where isnotempty(RequestName)
| project timestamp, RequestName, toint(Duration)
| summarize 
    Count = count(),
    AvgDuration = avg(toint(Duration)),
    MaxDuration = max(toint(Duration))
  by RequestName
| order by AvgDuration desc
```

**5. Idempotency Analysis**:
```kusto
// Track idempotency key usage
traces
| where timestamp > ago(24h)
| where message contains "Idempotency"
| project timestamp, message, operation_Id
| order by timestamp desc
```

## Integration Test Telemetry

### Test Configuration

**Automatic Test Marking**:
```csharp
public class TestTelemetryInitializer : ITelemetryInitializer
{
    public void Initialize(ITelemetry telemetry)
    {
        telemetry.Context.GlobalProperties["TestSource"] = "IntegrationTests";
        telemetry.Context.GlobalProperties["TestEnvironment"] = "Local";
        telemetry.Context.GlobalProperties["TestTimestamp"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");
    }
}
```

**Filter Test Data in Queries**:
```kusto
// Exclude test data from production analysis
requests
| where timestamp > ago(24h)
| where not(customDimensions.TestSource == "IntegrationTests")
| summarize count() by name
```

**View Only Test Data**:
```kusto
// Analyze integration test results
requests
| where timestamp > ago(24h)
| where customDimensions.TestSource == "IntegrationTests"
| project timestamp, name, duration, success, resultCode
| order by timestamp desc
```

## Troubleshooting Common Issues

### 1. No Telemetry in Application Insights

**Check Configuration**:
```bash
# Verify connection string is set
echo $APPLICATIONINSIGHTS_CONNECTION_STRING

# Check application logs
dotnet run --project src/IntegrationGateway.Api 2>&1 | grep -i "application insights"
```

**Expected Output**:
```
Application Insights configured with connection string: InstrumentationKey=...
```

### 2. Missing Performance Logs

**Verify MediatR Registration**:
```csharp
// Ensure PerformanceBehaviour is registered in DependencyInjection.cs
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceBehaviour<,>));
```

**Check Performance Configuration**:
```json
{
  "Performance": {
    "SlowRequestThresholdMs": 500,
    "CriticalRequestThresholdMs": 2000
  }
}
```

### 3. Sensitive Data in Logs

**Review Filtering Logic**:
```csharp
// Ensure sensitive keywords are properly configured
private static readonly string[] SensitiveKeywords = 
{
    "password", "token", "secret", "key", "credential"
    // Add your domain-specific sensitive terms
};
```

### 4. High Telemetry Volume

**Adjust Sampling**:
```json
{
  "ApplicationInsights": {
    "EnableAdaptiveSampling": true,
    "SamplingPercentage": 10.0  // Sample only 10% in high-volume scenarios
  }
}
```

## Best Practices

### 1. Structured Logging
- Use structured logging with named parameters
- Include correlation IDs in all logs
- Use appropriate log levels (Debug, Information, Warning, Error, Critical)

### 2. Performance Monitoring
- Set realistic performance thresholds
- Monitor both P95 and P99 percentiles
- Track business-specific metrics

### 3. Error Handling
- Log exceptions with full context
- Include user actions and request state
- Use correlation IDs to trace across services

### 4. Security
- Never log sensitive data (passwords, tokens, keys)
- Use allow-lists for safe properties
- Sanitize user input in logs

### 5. Application Insights
- Use meaningful cloud role names
- Add custom properties for filtering
- Create alerts for critical metrics
- Regularly review and optimize queries

---

*This guide provides comprehensive coverage of logging and telemetry in the Integration Gateway, enabling effective debugging and monitoring in production environments.*