# Testing Guide

## Test Overview

The project includes **34 tests** across two main categories:

### 🧪 **Unit Tests** (`/UnitTests/`)
- **Controller Tests**: Test MediatR integration, request validation, error handling
- **Service Tests**: Test business logic, concurrency, performance benchmarks
- **Isolated**: Use mocks/stubs, no external dependencies

### 🔗 **Integration Tests** (`/Integration/`)
- **Full Pipeline Tests**: Test complete HTTP request flow
- **External Dependencies**: Use WireMock for ERP/Warehouse services
- **Application Insights**: Test telemetry integration

## Test Structure

```
tests/IntegrationGateway.Tests/
├── UnitTests/
│   ├── Controllers/V1/
│   │   ├── GetProductsControllerTests.cs    # GET endpoint tests
│   │   └── CreateProductControllerTests.cs  # POST endpoint tests
│   └── Services/
│       └── IdempotencyConcurrencyTests.cs   # Performance & concurrency
└── Integration/
    ├── V1ProductsIntegrationTests.cs        # Full API integration
    └── TestApplicationInsightsConfiguration.cs # Telemetry setup
```

## Running Tests

### All Tests
```bash
# Run all unit + integration tests
dotnet test

# With detailed output
dotnet test --verbosity normal

# Show test names
dotnet test --logger "console;verbosity=detailed"
```

### Unit Tests Only
```bash
# Run all unit tests
dotnet test --filter "FullyQualifiedName~UnitTests"

# Run controller tests only
dotnet test --filter "FullyQualifiedName~Controllers"

# Run service tests only  
dotnet test --filter "FullyQualifiedName~Services"
```

### Integration Tests Only
```bash
# Run all integration tests
dotnet test --filter "FullyQualifiedName~Integration"

# Run specific integration test
dotnet test --filter "V1ProductsIntegrationTests"
```

### Performance Tests
```bash
# Run concurrency/performance benchmarks
dotnet test --filter "IdempotencyConcurrencyTests"

# Run specific performance test
dotnet test --filter "GetOrCreateOperationAsync_PerformanceBenchmark"
```

## Test Categories

### 📋 **Controller Unit Tests**
- **Purpose**: Test HTTP layer, MediatR integration, validation
- **Dependencies**: Mocked IMediator
- **Coverage**: Request/response handling, error scenarios

```bash
# Example tests:
# - GetProducts_WithDefaultParameters_ShouldReturnOkWithProductList
# - CreateProduct_WithValidRequest_ShouldReturnCreatedProduct  
# - CreateProduct_WithInvalidRequest_ShouldReturnBadRequest
```

### ⚡ **Service Unit Tests** 
- **Purpose**: Test business logic, concurrency safety, performance
- **Dependencies**: Real services with mocked external calls
- **Coverage**: Idempotency logic, high-concurrency scenarios

```bash
# Example tests:
# - GetOrCreateOperationAsync_HighConcurrency_OnlyOneNewOperation
# - GetOrCreateOperationAsync_PerformanceBenchmark
# - TTL_Expiration_Works_Correctly
```

### 🌐 **Integration Tests**
- **Purpose**: Test full HTTP pipeline with real dependencies
- **Dependencies**: WireMock servers for ERP/Warehouse
- **Coverage**: End-to-end workflows, external service integration

```bash
# Example tests:
# - GetProducts_Should_Return_Merged_Data_From_ERP_And_Warehouse
# - CreateProduct_Should_Send_Request_To_ERP_Service
# - ApplicationInsights_Telemetry_Is_Collected
```

## Code Coverage

### Generate Coverage Report
```bash
# Run tests with coverage collection
dotnet test --collect:"XPlat Code Coverage"

# Generate HTML report (requires reportgenerator)
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coverage-report" -reporttypes:Html
```

### View Coverage
```bash
# Open coverage report
open coverage-report/index.html  # macOS
start coverage-report/index.html # Windows
```

## Test Technologies

### 🛠️ **Testing Stack**
- **xUnit**: Test framework
- **Moq**: Mocking framework
- **FluentAssertions**: Readable assertions
- **WireMock.NET**: HTTP service mocking
- **WebApplicationFactory**: Integration testing
- **coverlet**: Code coverage

### 📊 **Performance Testing**
- **Concurrency Tests**: 100+ parallel requests
- **Benchmarking**: Stopwatch-based timing
- **Memory Analysis**: GC pressure monitoring
- **Throughput Metrics**: Operations per second

## Test Requirements

### Prerequisites for Integration Tests
```bash
# Integration tests require available ports:
# - 5051 (ERP WireMock)
# - 5052 (Warehouse WireMock)

# Ensure ports are available before running
netstat -an | grep "505[12]"  # Should show no results
```

### Environment Variables
```bash
# Optional: Configure Application Insights for integration tests
export APPLICATIONINSIGHTS_CONNECTION_STRING="your-connection-string"

# Run integration tests with telemetry
dotnet test --filter "Integration"
```

## Troubleshooting

### Common Issues

**1. Port Conflicts**
```bash
# Check if test ports are busy
lsof -i :5051  # ERP mock port
lsof -i :5052  # Warehouse mock port
```

**2. Slow Performance Tests**
```bash
# Performance tests may take 30-60 seconds
# This is normal for 100+ concurrent operations
dotnet test --filter "Performance" --logger "console;verbosity=detailed"
```

**3. Integration Test Failures**
```bash
# Ensure WireMock dependencies are available
dotnet restore
dotnet build
```

## Test Examples

### Quick Test Commands
```bash
# Fast feedback loop
dotnet test --filter "GetProducts"

# Full controller validation  
dotnet test --filter "Controllers" --verbosity normal

# Concurrency stress test
dotnet test --filter "HighConcurrency" 

# Complete integration validation
dotnet test --filter "Integration" --logger console
```

---

💡 **Tip**: Run unit tests during development for fast feedback, integration tests before commits.