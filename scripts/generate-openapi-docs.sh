#!/bin/bash

# Bash script to generate OpenAPI documentation
OUTPUT_DIR="${1:-../docs/api}"
BASE_URL="${2:-http://localhost:5000}"

echo "🚀 Generating OpenAPI documentation for Integration Gateway..."

# Create output directory if it doesn't exist
if [ ! -d "$OUTPUT_DIR" ]; then
    mkdir -p "$OUTPUT_DIR"
    echo "📁 Created output directory: $OUTPUT_DIR"
fi

echo "📡 Fetching OpenAPI specifications from $BASE_URL..."

# Function to check if a command exists
command_exists() {
    command -v "$1" >/dev/null 2>&1
}

# Download V1 OpenAPI specification
echo "📥 Downloading V1 specification..."
if curl -s -f "$BASE_URL/swagger/v1/swagger.json" -o "$OUTPUT_DIR/integration-gateway-v1.json"; then
    echo "✅ V1 specification saved to: $OUTPUT_DIR/integration-gateway-v1.json"
else
    echo "❌ Failed to download V1 specification"
    echo "Make sure the Integration Gateway is running at $BASE_URL"
    exit 1
fi

# Download V2 OpenAPI specification
echo "📥 Downloading V2 specification..."
if curl -s -f "$BASE_URL/swagger/v2/swagger.json" -o "$OUTPUT_DIR/integration-gateway-v2.json"; then
    echo "✅ V2 specification saved to: $OUTPUT_DIR/integration-gateway-v2.json"
else
    echo "❌ Failed to download V2 specification"
    exit 1
fi

# Generate YAML versions if swagger-codegen is available
if command_exists swagger-codegen; then
    echo "🔄 Converting to YAML format..."
    swagger-codegen generate -i "$OUTPUT_DIR/integration-gateway-v1.json" -l openapi-yaml -o "$OUTPUT_DIR/v1-yaml" >/dev/null 2>&1
    swagger-codegen generate -i "$OUTPUT_DIR/integration-gateway-v2.json" -l openapi-yaml -o "$OUTPUT_DIR/v2-yaml" >/dev/null 2>&1
    echo "✅ YAML versions generated"
else
    echo "⚠️  swagger-codegen not found. JSON versions only."
fi

# Generate HTML documentation if redoc-cli is available
if command_exists redoc-cli; then
    echo "📝 Generating HTML documentation..."
    redoc-cli build "$OUTPUT_DIR/integration-gateway-v1.json" --output "$OUTPUT_DIR/integration-gateway-v1.html" >/dev/null 2>&1
    redoc-cli build "$OUTPUT_DIR/integration-gateway-v2.json" --output "$OUTPUT_DIR/integration-gateway-v2.html" >/dev/null 2>&1
    echo "✅ HTML documentation generated"
else
    echo "⚠️  redoc-cli not found. Install with: npm install -g redoc-cli"
fi

# Create index file
cat > "$OUTPUT_DIR/README.md" << EOF
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
1. **JWT Bearer Token**: Add \`Authorization: Bearer <token>\` header
2. **Idempotency Key**: Add \`Idempotency-Key: <unique-id>\` header for POST/PUT operations

## Error Handling

The API follows RFC 7807 (Problem Details for HTTP APIs) for structured error responses.

Generated on: $(date -u +"%Y-%m-%d %H:%M:%S UTC")
EOF

echo "✅ Documentation index created: $OUTPUT_DIR/README.md"

echo ""
echo "🎉 OpenAPI documentation generation complete!"
echo "📁 Documentation available in: $OUTPUT_DIR"

# Display summary
echo ""
echo "📊 Summary:"
echo "- V1 OpenAPI JSON: $([ -f "$OUTPUT_DIR/integration-gateway-v1.json" ] && echo "✅ Created" || echo "❌ Missing")"
echo "- V2 OpenAPI JSON: $([ -f "$OUTPUT_DIR/integration-gateway-v2.json" ] && echo "✅ Created" || echo "❌ Missing")"
echo "- README created: $([ -f "$OUTPUT_DIR/README.md" ] && echo "✅ Created" || echo "❌ Missing")"

echo ""
echo "🌐 To view the interactive documentation:"
echo "   Open: $BASE_URL/swagger/index.html"
echo ""
echo "📖 To generate additional formats:"
echo "   - Install redoc-cli: npm install -g redoc-cli"
echo "   - Install swagger-codegen: https://github.com/swagger-api/swagger-codegen"