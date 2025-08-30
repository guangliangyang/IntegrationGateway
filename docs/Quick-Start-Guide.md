# Quick Start Guide

## Prerequisites
- **.NET 8.0 SDK** installed
- **Git** for cloning the repository

## Running the Services

### 1. Gateway (Main API)
```bash
# Navigate to project root
cd IntegrationGateway

# Start the Integration Gateway
dotnet run --project src/IntegrationGateway.Api
```

**Endpoints:**
- HTTP: `http://localhost:5050`
- HTTPS: `https://localhost:7000`
- Swagger: `https://localhost:7000/swagger`

### 2. ERP Stub (Mock ERP Service)
```bash
# In a new terminal
dotnet run --project stubs/ErpStub
```

**Endpoints:**
- HTTP: `http://localhost:5051`
- HTTPS: `https://localhost:7001`
- Swagger: `https://localhost:7001/swagger`

### 3. Warehouse Stub (Mock Warehouse Service)
```bash
# In a new terminal  
dotnet run --project stubs/WarehouseStub
```

**Endpoints:**
- HTTP: `http://localhost:5052`
- HTTPS: `https://localhost:7002`
- Swagger: `https://localhost:7002/swagger`

## Running Tests

### Unit Tests
```bash
# Run all tests
dotnet test

# Run with detailed output
dotnet test --verbosity normal

# Run with code coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Performance Tests
```bash
# Run performance/concurrency benchmarks
dotnet test --filter "IdempotencyConcurrencyTests"

# Run specific performance test
dotnet test --filter "GetOrCreateOperationAsync_PerformanceBenchmark"
```

## API Testing

### Using HTTP Files
The project includes `.http` files for quick API testing:

```bash
# Gateway API tests
src/IntegrationGateway.Api/IntegrationGateway.http

# ERP Stub tests  
stubs/ErpStub/ErpStub.http

# Warehouse Stub tests
stubs/WarehouseStub/WarehouseStub.http
```

### Quick API Tests
```bash
# Test Gateway health
curl http://localhost:5050/health

# Test V1 API
curl http://localhost:5050/api/v1/products

# Test V2 API
curl http://localhost:5050/api/v2/products

# Test ERP Stub
curl http://localhost:5051/api/products

# Test Warehouse Stub  
curl http://localhost:5052/api/stock
```

## Development Workflow

### 1. Start All Services
```bash
# Terminal 1: Start stubs first
dotnet run --project stubs/ErpStub

# Terminal 2: Start warehouse stub
dotnet run --project stubs/WarehouseStub  

# Terminal 3: Start gateway (will connect to stubs)
dotnet run --project src/IntegrationGateway.Api
```

### 2. Verify Setup
- Gateway Swagger: `https://localhost:7000/swagger`
- ERP Swagger: `https://localhost:7001/swagger`
- Warehouse Swagger: `https://localhost:7002/swagger`

### 3. Run Tests
```bash
# Verify everything works
dotnet test
```

## Configuration Notes

### Default Service URLs
The gateway is configured to connect to:
- **ERP Service**: `http://localhost:5051` (ErpStub)
- **Warehouse Service**: `http://localhost:5052` (WarehouseStub)

### Environment Configuration
For production deployment, update `appsettings.json`:
```json
{
  "ErpService": {
    "BaseUrl": "https://your-production-erp.com"
  },
  "WarehouseService": {
    "BaseUrl": "https://your-production-warehouse.com"  
  }
}
```

## Troubleshooting

### Port Conflicts
If ports are in use, check `Properties/launchSettings.json` in each project to change ports.

### Service Connection Issues
1. Verify stubs are running first
2. Check firewall/antivirus blocking connections
3. Ensure .NET 8.0 SDK is properly installed

### Performance Test Issues
Performance tests may take several minutes to complete - this is normal for concurrency benchmarks.

---

🚀 **You're ready to go!** The gateway will orchestrate calls between ERP and Warehouse stubs to provide a unified API.