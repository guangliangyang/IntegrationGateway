# Configuration Management Guide

## Overview

This guide explains how to manage configurations for the Integration Gateway, including externalization of settings, environment variable usage, and Azure Key Vault integration.

## Configuration Hierarchy

The application follows this configuration precedence (highest to lowest):

1. **Azure Key Vault** (for sensitive data)
2. **Environment Variables** (runtime overrides)
3. **appsettings.{Environment}.json** (environment-specific)
4. **appsettings.json** (defaults)

## Environment Variables

### Required Environment Variables (Production)

```bash
# Application Insights
APPLICATIONINSIGHTS_CONNECTION_STRING="InstrumentationKey=xxx;IngestionEndpoint=https://..."
APPLICATIONINSIGHTS_INSTRUMENTATION_KEY="your-instrumentation-key"

# Authentication
JWT_SECRET_KEY="your-super-secure-jwt-secret-key-at-least-32-chars"

# External Services
ERP_API_KEY="your-erp-service-api-key"
WAREHOUSE_API_KEY="your-warehouse-service-api-key"
ERP_BASE_URL="https://your-erp-service.com"
WAREHOUSE_BASE_URL="https://your-warehouse-service.com"

# Optional Environment Info
AZURE_REGION="East US"
BUILD_VERSION="1.0.0"
HOSTNAME="integration-gateway-prod-01"
```

### .NET Core Environment Variable Format

Use double underscores (`__`) to represent nested configuration:

```bash
# Circuit Breaker Settings
CircuitBreaker__FailureThreshold=10
CircuitBreaker__BreakDuration="00:02:00"
CircuitBreaker__MinimumThroughput=20

# HTTP Client Settings
HttpClient__UserAgent="IntegrationGateway/2.0"
HttpClient__DefaultConnectionTimeoutSeconds=45
HttpClient__MaxConnectionsPerServer=20

# Idempotency Settings
Idempotency__DefaultExpirationTime="2.00:00:00"
Idempotency__MaxConcurrentOperations=2000

# Business Mapping (JSON format for complex objects)
BusinessMapping__DefaultSupplier="Production Supplier Inc"
```

## Azure Key Vault Integration

### Key Vault Secrets Names

```
# Authentication
integration-gateway-jwt-secret
erp-service-api-key
warehouse-service-api-key

# Application Insights
applicationinsights-connection-string
applicationinsights-instrumentation-key

# Database (if applicable)
database-connection-string
```

### Adding Key Vault Support

1. Install Azure Key Vault packages:
```xml
<PackageReference Include="Azure.Extensions.AspNetCore.Configuration.Secrets" Version="1.2.2" />
<PackageReference Include="Azure.Identity" Version="1.10.0" />
```

2. Update Program.cs:
```csharp
if (!builder.Environment.IsDevelopment())
{
    var keyVaultUri = builder.Configuration["KeyVault:VaultUri"];
    if (!string.IsNullOrEmpty(keyVaultUri))
    {
        builder.Configuration.AddAzureKeyVault(
            new Uri(keyVaultUri),
            new DefaultAzureCredential());
    }
}
```

## Configuration Classes

### Available Configuration Sections

| Section | Purpose | Key Settings |
|---------|---------|-------------|
| `ApplicationInsights` | Telemetry configuration | ConnectionString, SamplingPercentage |
| `Idempotency` | Idempotency behavior | DefaultExpirationTime, SemaphoreTimeout |
| `HttpClient` | HTTP client settings | UserAgent, TimeoutSeconds |
| `CircuitBreaker` | Resilience patterns | FailureThreshold, BreakDuration |

### Configuration Validation

Add validation for critical configuration:

```csharp
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection("Jwt"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<ApplicationInsightsOptions>()
    .Bind(builder.Configuration.GetSection("ApplicationInsights"))
    .Validate(config => !string.IsNullOrEmpty(config.ConnectionString), 
              "Application Insights connection string is required")
    .ValidateOnStart();
```

## Security Best Practices

### ✅ Do's

- Store sensitive data in Azure Key Vault
- Use environment variables for runtime configuration
- Validate configuration at startup
- Use strong typing with IOptions pattern
- Set empty strings in appsettings.json for secrets
- Use different configurations per environment

### ❌ Don'ts

- Never commit secrets to source control
- Don't hardcode URLs or timeouts
- Avoid storing production secrets in appsettings files
- Don't use the same keys across environments

## Docker Configuration

### Environment Variables in Docker

```dockerfile
ENV APPLICATIONINSIGHTS_CONNECTION_STRING=""
ENV JWT_SECRET_KEY=""
ENV ERP_API_KEY=""
ENV WAREHOUSE_API_KEY=""
ENV ASPNETCORE_ENVIRONMENT=Production
```

### Docker Compose Example

```yaml
services:
  integration-gateway:
    image: integration-gateway:latest
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - APPLICATIONINSIGHTS_CONNECTION_STRING=${AI_CONNECTION_STRING}
      - JWT_SECRET_KEY=${JWT_SECRET}
      - ERP_API_KEY=${ERP_KEY}
      - WAREHOUSE_API_KEY=${WAREHOUSE_KEY}
      - CircuitBreaker__FailureThreshold=10
      - HttpClient__DefaultConnectionTimeoutSeconds=45
```

## Kubernetes Configuration

### ConfigMap Example

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: integration-gateway-config
data:
  CircuitBreaker__FailureThreshold: "10"
  CircuitBreaker__BreakDuration: "00:02:00"
  HttpClient__UserAgent: "IntegrationGateway/2.0"
  Idempotency__MaxConcurrentOperations: "2000"
```

### Secret Example

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: integration-gateway-secrets
type: Opaque
data:
  jwt-secret: <base64-encoded-secret>
  erp-api-key: <base64-encoded-key>
  warehouse-api-key: <base64-encoded-key>
```

## Monitoring Configuration Health

Use health checks to verify configuration:

```csharp
builder.Services.AddHealthChecks()
    .AddCheck("configuration", () =>
    {
        var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>();
        return !string.IsNullOrEmpty(jwtOptions?.SecretKey) 
            ? HealthCheckResult.Healthy("JWT configuration valid")
            : HealthCheckResult.Unhealthy("JWT secret key not configured");
    });
```

## Troubleshooting

### Common Issues

1. **Missing Environment Variables**: Check logs for configuration warnings
2. **Key Vault Access**: Ensure managed identity has proper permissions
3. **Configuration Binding**: Verify section names match exactly
4. **Validation Errors**: Check startup logs for configuration validation failures

### Debug Configuration

Enable configuration debugging:

```csharp
if (builder.Environment.IsDevelopment())
{
    var config = builder.Configuration as IConfigurationRoot;
    foreach (var provider in config.Providers)
    {
        Console.WriteLine($"Provider: {provider}");
    }
}
```