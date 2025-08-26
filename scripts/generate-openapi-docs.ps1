# PowerShell script to generate OpenAPI documentation
param(
    [string]$OutputDir = "../docs/api",
    [string]$BaseUrl = "http://localhost:5000"
)

Write-Host "Generating OpenAPI documentation for Integration Gateway..." -ForegroundColor Green

# Create output directory if it doesn't exist
if (!(Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force
    Write-Host "Created output directory: $OutputDir" -ForegroundColor Yellow
}

# Start the application in background (assuming it's already running)
Write-Host "Fetching OpenAPI specifications from $BaseUrl..." -ForegroundColor Blue

try {
    # Download V1 OpenAPI specification
    Write-Host "Downloading V1 specification..." -ForegroundColor Blue
    Invoke-WebRequest -Uri "$BaseUrl/swagger/v1/swagger.json" -OutFile "$OutputDir/integration-gateway-v1.json"
    Write-Host "✅ V1 specification saved to: $OutputDir/integration-gateway-v1.json" -ForegroundColor Green

    # Download V2 OpenAPI specification  
    Write-Host "Downloading V2 specification..." -ForegroundColor Blue
    Invoke-WebRequest -Uri "$BaseUrl/swagger/v2/swagger.json" -OutFile "$OutputDir/integration-gateway-v2.json"
    Write-Host "✅ V2 specification saved to: $OutputDir/integration-gateway-v2.json" -ForegroundColor Green

    # Generate YAML versions if possible
    if (Get-Command "swagger-codegen" -ErrorAction SilentlyContinue) {
        Write-Host "Converting to YAML format..." -ForegroundColor Blue
        swagger-codegen generate -i "$OutputDir/integration-gateway-v1.json" -l openapi-yaml -o "$OutputDir/v1-yaml"
        swagger-codegen generate -i "$OutputDir/integration-gateway-v2.json" -l openapi-yaml -o "$OutputDir/v2-yaml"
        Write-Host "✅ YAML versions generated" -ForegroundColor Green
    } else {
        Write-Host "⚠️  swagger-codegen not found. JSON versions only." -ForegroundColor Yellow
    }

    # Generate HTML documentation if possible
    if (Get-Command "redoc-cli" -ErrorAction SilentlyContinue) {
        Write-Host "Generating HTML documentation..." -ForegroundColor Blue
        redoc-cli build "$OutputDir/integration-gateway-v1.json" --output "$OutputDir/integration-gateway-v1.html"
        redoc-cli build "$OutputDir/integration-gateway-v2.json" --output "$OutputDir/integration-gateway-v2.html"
        Write-Host "✅ HTML documentation generated" -ForegroundColor Green
    } else {
        Write-Host "⚠️  redoc-cli not found. Install with: npm install -g redoc-cli" -ForegroundColor Yellow
    }

    # Create index file
    $indexContent = @"
# Integration Gateway API Documentation

This directory contains the complete OpenAPI documentation for the Integration Gateway API.

## Files

- **integration-gateway-v1.json**: OpenAPI 3.0 specification for API Version 1
- **integration-gateway-v2.json**: OpenAPI 3.0 specification for API Version 2
- **integration-gateway-v1.html**: Interactive HTML documentation for V1 (if generated)
- **integration-gateway-v2.html**: Interactive HTML documentation for V2 (if generated)

## Online Documentation

When the service is running, you can access the interactive Swagger UI at:

- V1 Documentation: [http://localhost:5000/swagger/index.html](http://localhost:5000/swagger/index.html)
- V1 OpenAPI JSON: [http://localhost:5000/swagger/v1/swagger.json](http://localhost:5000/swagger/v1/swagger.json)
- V2 OpenAPI JSON: [http://localhost:5000/swagger/v2/swagger.json](http://localhost:5000/swagger/v2/swagger.json)

## API Versions

### Version 1 (v1)
- Basic product management operations
- JWT authentication
- Idempotency support
- Comprehensive error handling

### Version 2 (v2)
- All V1 features
- Enhanced product information (supplier, tags, metadata)
- Improved response formats
- Backward compatible with V1

## Authentication

All write operations require:
1. **JWT Bearer Token**: Add `Authorization: Bearer <token>` header
2. **Idempotency Key**: Add `Idempotency-Key: <unique-id>` header for POST/PUT operations

## Error Handling

The API follows RFC 7807 (Problem Details for HTTP APIs) for structured error responses.

Generated on: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss UTC")
"@

    $indexContent | Out-File -FilePath "$OutputDir/README.md" -Encoding UTF8
    Write-Host "✅ Documentation index created: $OutputDir/README.md" -ForegroundColor Green

    Write-Host "`n🎉 OpenAPI documentation generation complete!" -ForegroundColor Green
    Write-Host "Documentation available in: $OutputDir" -ForegroundColor Cyan

} catch {
    Write-Host "❌ Error generating documentation: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Make sure the Integration Gateway is running at $BaseUrl" -ForegroundColor Yellow
    exit 1
}

# Display summary
Write-Host "`n📊 Summary:" -ForegroundColor Cyan
Write-Host "- V1 OpenAPI JSON: $(Test-Path "$OutputDir/integration-gateway-v1.json")" -ForegroundColor $(if (Test-Path "$OutputDir/integration-gateway-v1.json") { "Green" } else { "Red" })
Write-Host "- V2 OpenAPI JSON: $(Test-Path "$OutputDir/integration-gateway-v2.json")" -ForegroundColor $(if (Test-Path "$OutputDir/integration-gateway-v2.json") { "Green" } else { "Red" })
Write-Host "- README created: $(Test-Path "$OutputDir/README.md")" -ForegroundColor $(if (Test-Path "$OutputDir/README.md") { "Green" } else { "Red" })