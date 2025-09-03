# BlazorShop

[![.NET](https://github.com/unrealbg/BlazorShop/actions/workflows/dotnet.yml/badge.svg)](https://github.com/unrealbg/BlazorShop/actions/workflows/dotnet.yml)

BlazorShop is an open-source e-commerce application built with Blazor WebAssembly and ASP.NET Core (.NET 9). It follows a clean, layered architecture and provides a ready-to-extend foundation for real online stores.

## Table of Contents
- [Introduction](#introduction)
- [Who Is It For?](#who-is-it-for)
- [Features](#features)
- [Technologies Used](#technologies-used)
- [Requirements](#requirements)
- [Getting Started](#getting-started)
- [Project Structure](#project-structure)
- [API & Docs](#api--docs)
- [Screenshots](#screenshots)
- [Contributing](#contributing)
- [Demo](#demo)
- [License](#license)
- [Acknowledgements](#acknowledgements)

## Introduction
BlazorShop delivers a modern, responsive shopping experience with a Blazor WebAssembly frontend and a secure ASP.NET Core Web API backend. It includes product catalog, cart, checkout with multiple payment methods, order tracking, admin tooling, and more.

### Who Is It For?
- Small/medium businesses looking to bootstrap an online shop on .NET.
- Developers exploring Blazor WebAssembly, ASP.NET Core, and clean architecture.

## Features
- Authentication & Authorization
  - ASP.NET Core Identity, JWT access tokens + refresh flow
  - Email confirmation, password change, profile update
  - Role-based access (Admin/User)
  - Note: The first registered user becomes Admin; next users get User role.
- Catalog Management
  - Categories, products, product variants (size/stock), image upload
  - Product search/typeahead UI
- Cart & Checkout
  - Persistent cart (cookie), quantity updates, totals
  - Multiple payment methods: Stripe (card), PayPal, Cash on Delivery, Bank Transfer
  - Bank transfer instructions via email with order reference
- Orders & Tracking
  - Save checkout history; admin order list
  - Update shipping status, carrier tracking number and URL
- Newsletter
  - Email subscription with welcome email
- Admin Area
  - Manage categories, products, variants, orders, and shipping
- Developer Experience
  - OpenAPI/Swagger, Serilog logging, unit tests
  - Modern UI (Tailwind-style classes), toast notifications, Chart.js

## Technologies Used
- .NET 9, ASP.NET Core Web API
- Blazor WebAssembly (client)
- Entity Framework Core 9 + SQL Server
- ASP.NET Core Identity (email confirmation enabled)
- AutoMapper, FluentValidation
- Serilog
- Stripe & PayPal integrations
- Swashbuckle (Swagger/OpenAPI)
- xUnit, Moq (tests)
- Microsoft Aspire AppHost (local orchestrator)

## Requirements
- .NET 9 SDK or later
- SQL Server (LocalDB or full instance)
- Modern browser (WebAssembly capable)
- Optional: API keys and SMTP for payments/emails
  - Stripe Secret Key
  - PayPal settings (if applicable)
  - SMTP credentials

## Getting Started
1) Clone the repository

   ```bash
   git clone https://github.com/unrealbg/BlazorShop.git
   cd BlazorShop
   ```

2) Configure the API (appsettings or user-secrets)
- File: `BlazorShop.Presentation/BlazorShop.API/appsettings.json`
- Keys you may need to set/update:
  - `ConnectionStrings:DefaultConnection`
  - `Jwt: Key, Issuer, Audience`
  - `Stripe: SecretKey`
  - `BankTransfer: Iban, Beneficiary, BankName, AdditionalInfo`
  - `EmailSettings: From, DisplayName, SmtpServer, Port, UseSsl, Username, Password`

Tip: keep secrets out of source control via `dotnet user-secrets` for the API project.

3) Database
- The API applies EF Core migrations automatically on startup.
- Or apply manually from the solution root:

   ```bash
   dotnet ef database update --project BlazorShop.Infrastructure --startup-project BlazorShop.Presentation/BlazorShop.API
   ```

4) Run the app (pick one)
- Using the AppHost (recommended local orchestrator):

   ```bash
   dotnet run --project BlazorShop.AppHost
   ```

- Run projects separately (two terminals):

   ```bash
   dotnet run --project BlazorShop.Presentation/BlazorShop.API
   dotnet run --project BlazorShop.Presentation/BlazorShop.Web
   ```

Default dev URLs (may vary by environment):
- API: https://localhost:7094  
- Web: https://localhost:7258  
- The Web client calls the API at https://localhost:7094/api/

5) Tests

   ```bash
   dotnet test
   ```

## Project Structure
- **BlazorShop.Domain** – Core entities and contracts
- **BlazorShop.Application** – DTOs, services, validations
- **BlazorShop.Infrastructure** – EF Core, repositories, Identity, email, payments, logging
- **BlazorShop.Presentation/BlazorShop.API** – ASP.NET Core Web API controllers and configuration
- **BlazorShop.Presentation/BlazorShop.Web** – Blazor WebAssembly client
- **BlazorShop.Presentation/BlazorShop.Web.Shared** – Shared models/services used by the Web client
- **BlazorShop.AppHost** – Local orchestrator (Microsoft Aspire) to run API + Web together
- **BlazorShop.Tests** – Unit tests

## API & Docs
- Swagger UI available when API runs in Development at `/swagger`
- CORS allows localhost HTTP/HTTPS origins

## Screenshots
*(Screenshots table unchanged, already correct)*

## Contributing
1. Fork the repository.
2. Create a feature branch:

   ```bash
   git checkout -b feature/your-feature
   ```

3. Commit your changes:

   ```bash
   git commit -m "feat: add your feature"
   ```

4. Push and open a Pull Request.

## Demo
Live demo: https://shop.unrealbg.com

## License
MIT License. See the LICENSE file for details.

## Acknowledgements
- https://github.com/unrealbg – Creator and maintainer of BlazorShop.
