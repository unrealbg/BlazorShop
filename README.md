[![CI/CD](https://github.com/unrealbg/BlazorShop/actions/workflows/dotnet.yml/badge.svg)](https://github.com/unrealbg/BlazorShop/actions/workflows/dotnet.yml)
[![CodeQL](https://github.com/unrealbg/BlazorShop/actions/workflows/codeql.yml/badge.svg)](https://github.com/unrealbg/BlazorShop/actions/workflows/codeql.yml)
[![Quality Gate Status](https://img.shields.io/badge/Quality%20Gate-Passing-success)](https://github.com/unrealbg/BlazorShop)
[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](./LICENSE.txt)

## Table of Contents
- [Introduction](#introduction)
- [Architecture Overview](#architecture-overview)
- [Who Is It For?](#who-is-it-for)
- [Features](#features)
- [Technologies Used](#technologies-used)
- [Requirements](#requirements)
- [Getting Started](#getting-started)
  - [Local Development](#local-development)
  - [Docker Development](#docker-development)
  - [Development Container](#development-container)
- [Configuration](#configuration)
- [Observability & Monitoring](#observability--monitoring)
- [Screenshots](#screenshots)
- [Contributing](#contributing)
- [Demo](#demo)
- [License](#license)
- [Acknowledgements](#acknowledgements)

## Introduction
BlazorShop is an open-source e-commerce platform designed to provide an efficient and user-friendly solution for managing online stores. Built with Blazor WebAssembly and ASP.NET Core, it delivers a seamless experience for both administrators and customers while showcasing modern development practices including Clean Architecture, observability, and security best practices.

## Architecture Overview

BlazorShop follows **Clean Architecture** principles with a clear separation of concerns:

```
├── BlazorShop.Domain          # Core business entities and contracts
├── BlazorShop.Application     # Use cases and business logic
├── BlazorShop.Infrastructure  # Data access and external services
├── BlazorShop.Presentation    # UI layers
│   ├── BlazorShop.API        # ASP.NET Core Web API
│   ├── BlazorShop.Web        # Blazor WebAssembly client
│   └── BlazorShop.Web.Shared # Shared models and DTOs
├── BlazorShop.ServiceDefaults # .NET Aspire service defaults
├── BlazorShop.AppHost        # .NET Aspire orchestration
└── BlazorShop.Tests          # Unit and integration tests
```

### Key Design Principles
- **Clean Architecture**: Domain-driven design with clear dependency inversion
- **CQRS Pattern**: Command Query Responsibility Segregation for scalability
- **.NET Aspire**: Modern cloud-native application development
- **Observability First**: Comprehensive logging, metrics, and tracing
- **Security by Default**: HTTPS, HSTS, CORS, rate limiting, and more
- **Configuration-Driven**: All features configurable via appsettings.json

### Who Is It For?
BlazorShop is ideal for:
- Small to medium-sized businesses looking to manage their online store efficiently.
- Developers wanting to explore Blazor WebAssembly and clean architecture in real-world projects.

## Features
- **Product Management**: Add, edit, and remove products effortlessly.
- **Category Management**: Organize products into categories for better navigation and management.
- **Order Management**: Handle customer orders with an intuitive interface.
- **Customer Management**: Track customer information and their order history.
- **Customization**: Adaptable to your specific business needs.

## Technologies Used
- **Backend**: ASP.NET Core 9.0 Web API
- **Frontend**: Blazor WebAssembly
- **Database**: Microsoft SQL Server (MSSQL)
- **ORM**: Entity Framework Core 9.0
- **Mapping**: AutoMapper
- **API Documentation**: Swagger/OpenAPI 3.0
- **Authentication**: JWT Bearer tokens with ASP.NET Core Identity
- **Logging**: Serilog with structured logging
- **Observability**: OpenTelemetry (traces, metrics, logs)
- **Health Checks**: ASP.NET Core Health Checks with database monitoring
- **Containerization**: Docker with multi-stage builds
- **Orchestration**: .NET Aspire for cloud-native development
- **CI/CD**: GitHub Actions with matrix builds and code coverage
- **Security**: CodeQL analysis, Dependabot, rate limiting, CORS

## Requirements
Before getting started, ensure you have the following:
- .NET 9 SDK or later
- SQL Server instance (local or cloud)
- A web browser that supports WebAssembly (e.g., Chrome, Edge, Firefox)

## Getting Started

### Local Development

#### Prerequisites
- .NET 9.0 SDK or later
- SQL Server instance (LocalDB, SQL Server Express, or full SQL Server)
- A web browser that supports WebAssembly (Chrome, Edge, Firefox, Safari)

#### Setup Instructions

1. **Clone the repository**:
    ```bash
    git clone https://github.com/unrealbg/BlazorShop.git
    cd BlazorShop
    ```

2. **Configure the database**:
    - Update the connection string in `BlazorShop.Presentation/BlazorShop.API/appsettings.json`:
    ```json
    {
      "ConnectionStrings": {
        "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=BlazorShopDb;Trusted_Connection=True;MultipleActiveResultSets=true"
      }
    }
    ```

3. **Apply database migrations**:
    ```bash
    dotnet ef database update --project BlazorShop.Infrastructure --startup-project BlazorShop.Presentation/BlazorShop.API
    ```

4. **Build and run the application**:
    ```bash
    # Using .NET Aspire (recommended)
    dotnet run --project BlazorShop.AppHost
    
    # Or run components separately
    dotnet run --project BlazorShop.Presentation/BlazorShop.API
    dotnet run --project BlazorShop.Presentation/BlazorShop.Web
    ```

5. **Access the application**:
    - API: https://localhost:7094 (with Swagger UI)
    - Web UI: https://localhost:7258
    - Aspire Dashboard: https://localhost:15888 (when using AppHost)

### Docker Development

For a complete containerized development environment with SQL Server, observability tools, and the application:

1. **Using Docker Compose**:
    ```bash
    # Start all services
    docker-compose up -d
    
    # View logs
    docker-compose logs -f
    
    # Stop services
    docker-compose down
    ```

2. **Access the services**:
    - Web UI: http://localhost:3000
    - API: http://localhost:5000
    - API Documentation: http://localhost:5000/swagger
    - Jaeger Tracing: http://localhost:16686
    - Seq Logging: http://localhost:5341
    - SQL Server: localhost:1433 (sa/BlazorShop123!)

3. **Production builds**:
    ```bash
    # Build production images
    docker-compose -f docker-compose.yml -f docker-compose.prod.yml build
    
    # Run production environment
    docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d
    ```

### Development Container

For a consistent development environment with all tools pre-configured:

1. **Using VS Code Dev Containers**:
    - Install the "Dev Containers" extension in VS Code
    - Open the project in VS Code
    - Press `Ctrl+Shift+P` and select "Dev Containers: Reopen in Container"
    - Wait for the container to build and start

2. **Using GitHub Codespaces**:
    - Click "Code" > "Create codespace on main" in the GitHub repository
    - Wait for the environment to initialize

The dev container includes:
- .NET 9.0 SDK
- Node.js LTS
- Git and GitHub CLI
- Docker-in-Docker
- Pre-configured VS Code extensions
- Port forwarding for all services
## Configuration

BlazorShop uses a configuration-first approach where most features can be enabled/disabled and customized via `appsettings.json`. Here are the key configuration sections:

### Security Configuration
```json
{
  "Security": {
    "RequireHttps": true,
    "EnableHsts": true,
    "EnableCorsPolicy": true,
    "AllowedOrigins": ["https://localhost:7258", "http://localhost:3000"],
    "EnableRateLimiting": true
  }
}
```

### Logging Configuration
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "theme": "Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme::Code, Serilog.Sinks.Console"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/blazorshop-.txt",
          "rollingInterval": "Day"
        }
      }
    ]
  }
}
```

### Rate Limiting Configuration
```json
{
  "RateLimiting": {
    "GlobalPolicy": {
      "PermitLimit": 100,
      "Window": "00:01:00",
      "QueueLimit": 10
    }
  }
}
```

### Health Checks Configuration
```json
{
  "HealthChecks": {
    "EnableDatabaseCheck": true,
    "DatabaseCheckTimeout": "00:00:05"
  }
}
```

## Observability & Monitoring

BlazorShop includes comprehensive observability features built on industry standards:

### Structured Logging
- **Serilog** with enrichers for correlation IDs, machine name, process ID, and thread ID
- **Request logging** with performance metrics and user context
- **Error tracking** with structured exception details and correlation
- **Log levels** configurable per namespace

### Distributed Tracing
- **OpenTelemetry** integration with automatic instrumentation
- **Trace correlation** across HTTP requests and database calls
- **Performance monitoring** for API endpoints and dependencies
- **Jaeger integration** for trace visualization (in development)

### Metrics & Health Monitoring
- **Health checks** for application readiness and liveness
- **Database connectivity** monitoring with configurable timeouts
- **Custom metrics** for business KPIs and performance counters
- **Prometheus-compatible** metrics endpoint

### Error Handling
- **Global exception handling** with RFC 7807 ProblemDetails
- **Correlation IDs** for request tracing across services
- **Structured error responses** with detailed context
- **Security-focused** error messages (no sensitive data exposure)

### Endpoints
- `/health/live` - Liveness probe (application is running)
- `/health/ready` - Readiness probe (application can serve traffic)
- `/health` - Complete health status (development only)

### External Integration
- **OTLP export** to external observability platforms (configurable)
- **Seq integration** for log aggregation and search
- **Jaeger tracing** for distributed trace analysis
- **Prometheus metrics** for monitoring and alerting

## Screenshots

### User Home View
![User Home View](https://vps.unrealbg.com/blazorblog/UserHomeView.png)

### Register Page
![Register Page](https://vps.unrealbg.com/blazorblog/RegisterPage.png)

### Login Page
![Login Page](https://vps.unrealbg.com/blazorblog/LoginPage.png)

### User Dropdown Menu
![User Dropdown Menu](https://vps.unrealbg.com/blazorblog/UserDropDown.png)

### My Profile Page
![My Profile Page](https://vps.unrealbg.com/blazorblog/MyProfilePage.png)

### Change Password
![Change Password](https://vps.unrealbg.com/blazorblog/ChangePassword.png)

### User Adds Product to Cart Once
![User Adds Product to Cart Once](https://vps.unrealbg.com/blazorblog/UserAddProductToCardOnce.png)

### User Adds Same Product Twice
![User Adds Same Product Twice](https://vps.unrealbg.com/blazorblog/UserAddSameProductTwice.png)

### See More Product Details
![See More Product Details](https://vps.unrealbg.com/blazorblog/SeeMore.png)

### User Cart
![User Cart](https://vps.unrealbg.com/blazorblog/UserCart.png)

### User Select Payment Method
![User Select Payment Method](https://vps.unrealbg.com/blazorblog/UserSelectPaymentMethod.png)

### Payment Process
![Payment Process](https://vps.unrealbg.com/blazorblog/Paying.png)

### Payment Success
![Payment Success](https://vps.unrealbg.com/blazorblog/PaymentSuccess.png)

### Add Category
![Add Category](https://vps.unrealbg.com/blazorblog/AddCategory.png)

### Add Category Confirmation
![Add Category Confirmation](https://vps.unrealbg.com/blazorblog/AddCategory2.png)

### Product Management Page
![Product Management Page](https://vps.unrealbg.com/blazorblog/AddProductPage.png)

### Add Product to Category
![Add Product to Category](https://vps.unrealbg.com/blazorblog/AddProductToCategory.png)

### Product Added Successfully
![Product Added Successfully](https://vps.unrealbg.com/blazorblog/ProductAddedSusscessfully.png)

### Delete Category Confirmation
![Delete Category Confirmation](https://vps.unrealbg.com/blazorblog/DeleteCategoty.png)

### Category Deletion Success
![Category Deletion Success](https://vps.unrealbg.com/blazorblog/CategoryDeleteSuccessfully.png)

### Sales Page
![Sales Page](https://vps.unrealbg.com/blazorblog/SalesPage.png)

## Contributing
We welcome contributions to BlazorShop! Here’s how you can get involved:
1. Fork the repository.
2. Create a new branch:
    ```sh
    git checkout -b feature-branch
    ```
3. Make your changes and commit them:
    ```sh
    git commit -m 'Add new feature'
    ```
4. Push to your branch:
    ```sh
    git push origin feature-branch
    ```
5. Open a pull request.

## Demo
Check out a live demo of BlazorShop [shop.unrealbg.com](https://shop.unrealbg.com).

## License
BlazorShop is licensed under the MIT License. See the [LICENSE](./LICENSE) file for more details.

## Acknowledgements
- **[unrealbg](https://github.com/unrealbg)**: The creator and main contributor of BlazorShop.
