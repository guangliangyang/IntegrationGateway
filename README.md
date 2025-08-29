# Integration Gateway

## Project Overview

**What**: Integration Gateway API for orchestrating ERP and Warehouse systems

**Why**: Production-ready enterprise integration platform showcasing coding excellence and Azure-native patterns

**Key Features**: API versioning, resilience patterns, idempotency, clean architecture, enterprise coding standards

## Quick Start

**Prerequisites**: .NET 8.0 SDK

**Clone and Run**: 
```bash
git clone https://github.com/guangliangyang/IntegrationGateway.git
cd IntegrationGateway
dotnet run --project src/IntegrationGateway.Api
```

**Detailed Guide**: See [Quick-Start-Guide.md](docs/Quick-Start-Guide.md)

## Architecture Highlights

- **API Versioning**: V1/V2 with inheritance pattern for zero-breaking-change evolution
- **Resilience Patterns**: 2 retries, 15s timeout, 5-failure circuit breaker with Polly
- **Idempotency**: 15-minute TTL with 3s fast-fail semantics for exactly-once operations
- **Clean Architecture**: CQRS/MediatR with pipeline behaviors for cross-cutting concerns
- **Caching Strategy**: 5-second TTL in-memory caching with type-safe configuration
- **Security**: Azure Key Vault integration with comprehensive SSRF protection

## Project Structure

```
IntegrationGateway/
├── src/                     # Source code
│   ├── IntegrationGateway.Api/      # Web API layer
│   ├── IntegrationGateway.Application/  # CQRS handlers & behaviors  
│   ├── IntegrationGateway.Models/       # DTOs and domain models
│   └── IntegrationGateway.Services/     # Business services
├── stubs/                   # Mock services for development
├── tests/                   # Unit & integration tests
└── docs/                    # Technical documentation
```

## Technology Stack

### Core Framework

- **.NET 8.0** + **ASP.NET Core** - High-concurrency async/await foundation
- **MediatR** - CQRS pattern with pipeline behaviors for cross-cutting concerns
- **Polly** - Production-ready resilience patterns (retry, circuit breaker, timeout)
- **Azure Key Vault** - Enterprise-grade secret management with DefaultAzureCredential
- **Application Insights** - Comprehensive observability with dependency tracking
- **xUnit + Moq** - Complete testing ecosystem with integration test support

### Why C# over Go/Node.js/Python?

**Practical Considerations**:
- **Mature ecosystem**: Rich enterprise-grade libraries for integration, authentication, monitoring
- **Team expertise**: Most enterprises have existing .NET teams, reducing learning curve and hiring challenges
- **Common approach**: Demonstrates best practices with mainstream technology for broader understanding

**Production Flexibility**:
- Azure supports all languages: Go, Python, Node.js can be deployed seamlessly
- Universal patterns: CQRS, resilience patterns, API versioning concepts apply across languages
- Future adaptability: Choose optimal tech stack based on specific requirements (performance, team skills)

## API Endpoints Preview

- **V1**: `/api/v1/products` - Core product operations
- **V2**: `/api/v2/products` - Enhanced with additional fields + batch operations  
- **Swagger UI**: `https://localhost:7000/swagger`

## Documentation Links

- [Quick Start Guide](docs/Quick-Start-Guide.md)
- [Testing Guide](docs/Testing-Guide.md)
- [API Multi-Versioning Technical Implementation](docs/API-Multi-Versioning-Technical-Implementation.md)
- [Cross-Cutting Concerns Strategy](docs/Cross-Cutting-Concerns-Strategy.md)
- [Framework & Technology Rationale](docs/Framework-Technology-Rationale.md)
- [Design Answers](answers/DESIGN.md)
- [Code Review Results](answers/CodeReview.md)

## Development Workflow

- **Running tests**: `dotnet test`
- **Starting services**: See [Quick Start Guide](docs/Quick-Start-Guide.md)
- **API testing**: Use `.http` files in project folders

## Coding Style & Standards

- **Clean Architecture**: Domain-driven design with clear separation of concerns
- **SOLID Principles**: Dependency injection, single responsibility, open-closed principle
- **Async/Await Patterns**: Proper async implementation for high-throughput scenarios
- **Error Handling**: Comprehensive exception handling with structured logging
- **Testing Excellence**: Unit tests, integration tests, and performance benchmarks
- **Security First**: Input validation, authentication, SSRF protection

## Production Considerations

### Current Implementation

- **Configuration**: Azure Key Vault integration for secrets
- **Security**: JWT authentication, SSRF protection, input validation  
- **Monitoring**: Application Insights telemetry and health checks
- **Performance**: High-concurrency support with async patterns

### Azure Production Upgrades

When scaling to enterprise production environments, consider these Azure services:

- **API Management**: Centralized API gateway with throttling, caching, analytics
- **Redis Cache**: Distributed caching for multi-instance scenarios
- **Azure Front Door**: Global load balancing and CDN capabilities
- **Azure Functions**: Serverless event processing for asynchronous workflows
- **Service Bus**: Enterprise messaging for event-driven architecture
- **Container Apps**: Microservices deployment with auto-scaling

## Performance Characteristics

- **Request Timeout**: 15 seconds with 2 retry attempts
- **Circuit Breaker**: Opens after 5 failures, 2-minute recovery window
- **Caching**: 5-second TTL for reduced upstream load
- **Idempotency**: 50 concurrent operations with 3-second fast-fail timeout
- **Concurrency**: Thread-safe operations with SemaphoreSlim coordination
- **Memory**: Efficient resource management with automatic cleanup

## Security Features

- **Authentication**: JWT Bearer token validation
- **Authorization**: Role-based access control
- **Input Validation**: Comprehensive request validation and sanitization
- **SSRF Protection**: URL validation and allowlist filtering
- **Rate Limiting**: Configurable throttling per client
- **Secrets Management**: Azure Key Vault integration

## Monitoring & Observability

- **Structured Logging**: JSON format with correlation IDs across MediatR pipeline
- **Business Metrics**: Cache hit ratios, circuit breaker states, operation durations
- **Health Checks**: Upstream service connectivity and system health monitoring
- **Distributed Tracing**: End-to-end request correlation with Application Insights
- **Pipeline Behaviors**: Request/response logging with performance benchmarks
- **Custom Telemetry**: Integration-specific metrics for troubleshooting

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Make your changes with comprehensive tests
4. Commit your changes (`git commit -m 'Add amazing feature'`)
5. Push to the branch (`git push origin feature/amazing-feature`)
6. Open a Pull Request

## Acknowledgments

- **Clean Architecture Pattern**: Inspired by [Jason Taylor's Clean Architecture Template](https://github.com/jasontaylordev/CleanArchitecture) - excellent foundation for .NET enterprise applications
- **Development Assistance**: Built with [Claude AI](https://claude.ai) assistance for code generation, architecture decisions, and best practices implementation
- **Open Source Libraries**: Grateful to the maintainers of MediatR, Polly, FluentValidation, and the entire .NET ecosystem

## License

This project is licensed under the MIT License - see the LICENSE file for details.

---

*This project demonstrates enterprise-grade integration patterns suitable for production Azure deployments.*