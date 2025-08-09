# BlazorShop Project Structure

This document provides an overview of the BlazorShop project structure and architecture.

## Solution Structure

```
BlazorShop/
├── BlazorShop.sln                      # Solution file
├── global.json                         # .NET SDK version specification
├── Directory.Build.props               # Shared MSBuild properties
├── Directory.Packages.props            # Central Package Management
├── .editorconfig                       # Code style configuration
├── stylecop.ruleset                   # StyleCop analyzer rules
├── docker-compose.yml                 # Development environment
├── docker-compose.prod.yml            # Production overrides
├── .dockerignore                      # Docker build exclusions
├── .devcontainer/                     # VS Code dev container
│   └── devcontainer.json
├── .github/                           # GitHub Actions & configuration
│   ├── workflows/
│   │   ├── dotnet.yml                # CI/CD pipeline
│   │   └── codeql.yml                # Security analysis
│   └── dependabot.yml               # Dependency updates
├── BlazorShop.Domain/                 # Core domain layer
├── BlazorShop.Application/            # Application services layer
├── BlazorShop.Infrastructure/         # Infrastructure layer
├── BlazorShop.Presentation/           # Presentation layer
│   ├── BlazorShop.API/               # ASP.NET Core Web API
│   │   ├── Controllers/
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   └── Dockerfile
│   ├── BlazorShop.Web/               # Blazor WebAssembly
│   │   ├── Pages/
│   │   ├── Components/
│   │   ├── wwwroot/
│   │   └── Dockerfile
│   └── BlazorShop.Web.Shared/        # Shared models/DTOs
├── BlazorShop.ServiceDefaults/        # .NET Aspire service defaults
├── BlazorShop.AppHost/                # .NET Aspire orchestration
└── BlazorShop.Tests/                  # Unit and integration tests
```

## Architecture Overview

BlazorShop follows **Clean Architecture** principles with clear separation of concerns:

### Core Layers (inner layers)
- **Domain**: Core business entities and domain logic
- **Application**: Use cases and business rules

### Infrastructure Layers (outer layers)  
- **Infrastructure**: Data access, external services, cross-cutting concerns
- **Presentation**: User interfaces (Web API, Blazor WebAssembly)

### Cross-Cutting Concerns
- **ServiceDefaults**: Shared configuration and services (.NET Aspire)
- **AppHost**: Application orchestration and service discovery

## Key Technologies

### Backend Stack
- **.NET 9.0**: Latest .NET version with performance improvements
- **ASP.NET Core**: Web API framework with built-in dependency injection
- **Entity Framework Core**: ORM for database access
- **SQL Server**: Primary database

### Frontend Stack
- **Blazor WebAssembly**: Client-side web UI framework
- **C#**: Single language across full stack
- **HTML/CSS**: Standard web technologies

### Observability Stack
- **Serilog**: Structured logging with multiple sinks
- **OpenTelemetry**: Distributed tracing and metrics
- **Health Checks**: Application health monitoring
- **.NET Aspire**: Cloud-native application development

### Development Tools
- **Docker**: Containerization for development and production
- **GitHub Actions**: CI/CD automation
- **CodeQL**: Security analysis
- **Dependabot**: Automated dependency updates
- **StyleCop**: Code style analysis

## Configuration Management

### Centralized Configuration
- **Directory.Build.props**: Shared MSBuild properties across all projects
- **Directory.Packages.props**: Central Package Management for consistent versions
- **.editorconfig**: Code style and formatting rules

### Environment-Specific Settings
- **appsettings.json**: Base configuration
- **appsettings.Development.json**: Development overrides
- **appsettings.Production.json**: Production overrides
- **docker-compose.override.yml**: Local Docker overrides

## Development Workflows

### Local Development
1. **Native**: Use .NET SDK and local SQL Server
2. **Docker**: Use docker-compose for full environment
3. **Dev Container**: Use VS Code dev containers for consistency

### CI/CD Pipeline
1. **Build**: Matrix builds on Linux and Windows
2. **Test**: Run unit and integration tests with coverage
3. **Analyze**: CodeQL security analysis
4. **Deploy**: Container-ready applications

## Security Features

### Application Security
- **JWT Authentication**: Bearer token authentication
- **ASP.NET Core Identity**: User management
- **HTTPS Enforcement**: SSL/TLS by default
- **CORS Policy**: Configurable cross-origin policies
- **Rate Limiting**: Protect against abuse

### Infrastructure Security
- **Non-root Containers**: Security-hardened Docker images
- **Secret Management**: Environment variable configuration
- **Health Checks**: Application monitoring
- **Dependency Scanning**: Automated vulnerability detection

## Observability Features

### Logging
- **Structured Logging**: JSON-formatted logs with correlation IDs
- **Multiple Sinks**: Console, file, and optional external systems
- **Request Logging**: HTTP request/response logging with performance metrics

### Tracing
- **Distributed Tracing**: OpenTelemetry integration
- **Correlation IDs**: Request tracking across services
- **Performance Monitoring**: Automatic instrumentation

### Monitoring
- **Health Checks**: Liveness and readiness probes
- **Metrics**: Application and infrastructure metrics
- **Error Tracking**: Centralized error handling with ProblemDetails

## Getting Started

See the main [README.md](README.md) for detailed setup instructions including:
- Local development setup
- Docker environment setup
- Dev container usage
- Configuration options