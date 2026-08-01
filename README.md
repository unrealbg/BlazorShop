# BlazorShop

[![CI](https://github.com/unrealbg/BlazorShop/actions/workflows/ci.yml/badge.svg)](https://github.com/unrealbg/BlazorShop/actions/workflows/ci.yml)

BlazorShop is an open-source e-commerce application built on .NET 10 with an ASP.NET Core Web API backend, a server-rendered Blazor Web App public storefront, and an existing Blazor WebAssembly client for admin and legacy interactive flows. It follows a clean, layered architecture and provides a ready-to-extend foundation for real online stores.

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
BlazorShop delivers a modern shopping experience with a server-rendered public storefront, a secure ASP.NET Core Web API backend, and a separate Blazor WebAssembly client that continues to host admin tooling and legacy interactive storefront flows. It includes product catalog, cart, checkout with multiple payment methods, order tracking, admin tooling, and more.

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
- Public SEO Storefront
  - Server-rendered product and category routes (`/product/{slug}` and `/category/{slug}`)
  - Published-only public catalog exposure and route-based metadata rendering
- Cart & Checkout
  - Persistent cart (cookie), quantity updates, totals
  - Multiple payment methods: Stripe (card), Cash on Delivery, Bank Transfer
  - Bank transfer instructions via email with order reference
- Orders & Tracking
  - Save checkout history; admin order list
  - Update shipping status, carrier tracking number and URL
- Newsletter
  - Email subscription with welcome email
- Admin Area
  - Dashboard, products, categories, variants, orders, users, inventory, SEO, redirects, settings, and audit log
  - User role editing, lock/unlock, email confirmation, password-change requirement flag, and guarded admin safety checks
  - Operational store/order/notification settings without exposing SMTP passwords or API secrets
  - Admin audit trail for sensitive catalog, SEO, redirect, order, user, settings, and inventory operations
  - Inventory overview with low/out-of-stock filtering and product/variant stock updates
- Developer Experience
  - OpenAPI/Swagger, Serilog logging, unit tests, GitHub Actions CI
  - Modern UI (Tailwind-style classes), toast notifications, Chart.js

## Technologies Used
- .NET 10, ASP.NET Core Web API
- Blazor Web App (server-rendered public storefront)
- Blazor WebAssembly (existing admin and legacy interactive client)
- Entity Framework Core 10 + PostgreSQL
- ASP.NET Core Identity (email confirmation enabled)
- AutoMapper, FluentValidation
- Serilog
- Stripe integration
- Swashbuckle (Swagger/OpenAPI)
- xUnit, Moq (tests)
- Microsoft Aspire AppHost (local orchestrator)

## Requirements
- .NET 10 SDK or later
- Docker Desktop or another compatible container runtime (recommended for `BlazorShop.AppHost` and `compose.production.yml`)
- PostgreSQL if you run the API outside the AppHost-provisioned database
- Modern browser (WebAssembly capable)
- Optional: API keys and SMTP for payments/emails
  - Stripe Secret Key
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

- Run projects separately (two or three terminals, depending on what you need):

   ```bash
   dotnet run --project BlazorShop.Presentation/BlazorShop.API
  dotnet run --project BlazorShop.Presentation/BlazorShop.Storefront
   dotnet run --project BlazorShop.Presentation/BlazorShop.Web
   ```

Default dev URLs (may vary by environment):
- API: https://localhost:7094  
- Storefront: ASP.NET Core Kestrel/AppHost-assigned URL  
- Web: https://localhost:7258  
- The Storefront and Web clients call the API at https://localhost:7094/api/ by default unless overridden in configuration.

Runtime notes:
- Standalone Storefront still serves its own static assets such as `/css/site.css` and `/icon-192.png`.
- Standalone and AppHost Storefront runs now expose crawl documents at `/sitemap.xml` and `/robots.txt` for the published public route surface.
- With the API unavailable, static informational Storefront pages such as `/about-us`, `/privacy`, `/faq`, and `/terms` still return `200`, while catalog-backed routes such as `/`, `/new-releases`, `/todays-deals`, `/category/{slug}`, and `/product/{slug}` return `503`.
- With the API available, Storefront slug routes return `200` for published content and `404` for unknown slugs.
- AppHost remains the easiest way to verify the full local stack because it runs API + Storefront + Web together.

5) Tests

   ```bash
    dotnet test BlazorShop.sln -c Release
   ```

## Project Structure
- **BlazorShop.Domain** – Core entities and contracts
- **BlazorShop.Application** – DTOs, services, validations
- **BlazorShop.Infrastructure** – EF Core, repositories, Identity, email, payments, logging
- **BlazorShop.Presentation/BlazorShop.API** – ASP.NET Core Web API controllers and configuration
- **BlazorShop.Presentation/BlazorShop.Storefront** – Server-rendered Blazor Web App public storefront
- **BlazorShop.Presentation/BlazorShop.Web** – Existing Blazor WebAssembly admin and legacy interactive client
- **BlazorShop.Presentation/BlazorShop.Web.Shared** – Shared models/services used by the Web client
- **BlazorShop.AppHost** – Local orchestrator (Microsoft Aspire) to run API + Storefront + Web together
- **BlazorShop.Tests** – Unit tests

## API & Docs
- Swagger UI available when API runs in Development at `/swagger`
- CORS is configuration-driven; localhost origins work out of the box in Development
- Production deployment reference: `docs/production-runbook.md`, `docs/production.appsettings.example.json`, `docs/storefront.production.appsettings.example.json`, and `compose.production.yml`

## Screenshots
Generated from the local seeded development stack. Source files live in
`docs/screenshots/`, with route and viewport metadata in
`docs/screenshots/manifest.json`.

### Public Storefront
<table>
  <tr>
    <td align="center">
      <img src="docs/screenshots/storefront-home.jpg" width="260" alt="Public storefront home page"/><br>
      <small>Storefront Home</small>
    </td>
    <td align="center">
      <img src="docs/screenshots/storefront-new-releases.jpg" width="260" alt="Public storefront new releases page"/><br>
      <small>New Releases</small>
    </td>
    <td align="center">
      <img src="docs/screenshots/storefront-todays-deals.jpg" width="260" alt="Public storefront today's deals page"/><br>
      <small>Today's Deals</small>
    </td>
  </tr>
  <tr>
    <td align="center">
      <img src="docs/screenshots/storefront-category-sneakers.jpg" width="260" alt="Public storefront category page"/><br>
      <small>Category</small>
    </td>
    <td align="center">
      <img src="docs/screenshots/storefront-product-metro-runner.jpg" width="260" alt="Public storefront product detail page"/><br>
      <small>Product Detail</small>
    </td>
    <td align="center">
      <img src="docs/screenshots/storefront-cart.jpg" width="260" alt="Public storefront cart page"/><br>
      <small>Cart</small>
    </td>
  </tr>
  <tr>
    <td align="center">
      <img src="docs/screenshots/storefront-about.jpg" width="260" alt="Public storefront about page"/><br>
      <small>About</small>
    </td>
    <td align="center">
      <img src="docs/screenshots/storefront-customer-service.jpg" width="260" alt="Public storefront customer service page"/><br>
      <small>Customer Service</small>
    </td>
    <td align="center">
      <img src="docs/screenshots/storefront-faq.jpg" width="260" alt="Public storefront FAQ page"/><br>
      <small>FAQ</small>
    </td>
  </tr>
  <tr>
    <td align="center">
      <img src="docs/screenshots/storefront-account-menu.jpg" width="260" alt="Public storefront account menu"/><br>
      <small>Account Menu</small>
    </td>
    <td align="center">
      <img src="docs/screenshots/storefront-mobile-menu.jpg" width="150" alt="Public storefront mobile menu"/><br>
      <small>Mobile Menu</small>
    </td>
  </tr>
</table>

### Account and Access
<table>
  <tr>
    <td align="center">
      <img src="docs/screenshots/web-workspace-entry.jpg" width="260" alt="Workspace access entry page"/><br>
      <small>Workspace Access</small>
    </td>
    <td align="center">
      <img src="docs/screenshots/auth-login.jpg" width="260" alt="Sign in page"/><br>
      <small>Sign In</small>
    </td>
    <td align="center">
      <img src="docs/screenshots/auth-register.jpg" width="260" alt="Register page"/><br>
      <small>Register</small>
    </td>
  </tr>
  <tr>
    <td align="center">
      <img src="docs/screenshots/account-dashboard.jpg" width="260" alt="Customer account dashboard"/><br>
      <small>Account Dashboard</small>
    </td>
    <td align="center">
      <img src="docs/screenshots/account-orders.jpg" width="260" alt="Customer account orders page"/><br>
      <small>Orders</small>
    </td>
    <td align="center">
      <img src="docs/screenshots/account-notifications.jpg" width="260" alt="Customer account notifications page"/><br>
      <small>Notifications</small>
    </td>
  </tr>
  <tr>
    <td align="center">
      <img src="docs/screenshots/account-profile.jpg" width="260" alt="Customer account profile page"/><br>
      <small>Profile</small>
    </td>
    <td align="center">
      <img src="docs/screenshots/account-settings.jpg" width="260" alt="Customer account settings page"/><br>
      <small>Settings</small>
    </td>
    <td align="center">
      <img src="docs/screenshots/account-checkout.jpg" width="260" alt="Customer account checkout page"/><br>
      <small>Checkout</small>
    </td>
  </tr>
  <tr>
    <td align="center">
      <img src="docs/screenshots/account-mobile-menu.jpg" width="150" alt="Customer account mobile menu"/><br>
      <small>Mobile Menu</small>
    </td>
  </tr>
</table>

### Admin Operations
<table>
  <tr>
    <td align="center">
      <img src="docs/screenshots/admin-dashboard.jpg" width="260" alt="Admin operations dashboard"/><br>
      <small>Dashboard</small>
    </td>
    <td align="center">
      <img src="docs/screenshots/admin-products.jpg" width="260" alt="Admin products page"/><br>
      <small>Products</small>
    </td>
    <td align="center">
      <img src="docs/screenshots/admin-product-add-modal.jpg" width="260" alt="Admin add product modal"/><br>
      <small>Add Product Modal</small>
    </td>
  </tr>
  <tr>
    <td align="center">
      <img src="docs/screenshots/admin-categories.jpg" width="260" alt="Admin categories page"/><br>
      <small>Categories</small>
    </td>
    <td align="center">
      <img src="docs/screenshots/admin-category-add-modal.jpg" width="260" alt="Admin add category modal"/><br>
      <small>Add Category Modal</small>
    </td>
    <td align="center">
      <img src="docs/screenshots/admin-inventory.jpg" width="260" alt="Admin inventory page"/><br>
      <small>Inventory</small>
    </td>
  </tr>
  <tr>
    <td align="center">
      <img src="docs/screenshots/admin-orders.jpg" width="260" alt="Admin orders page"/><br>
      <small>Orders</small>
    </td>
    <td align="center">
      <img src="docs/screenshots/admin-users.jpg" width="260" alt="Admin users page"/><br>
      <small>Users</small>
    </td>
    <td align="center">
      <img src="docs/screenshots/admin-seo.jpg" width="260" alt="Admin SEO page"/><br>
      <small>SEO</small>
    </td>
  </tr>
  <tr>
    <td align="center">
      <img src="docs/screenshots/admin-redirects.jpg" width="260" alt="Admin redirects page"/><br>
      <small>Redirects</small>
    </td>
    <td align="center">
      <img src="docs/screenshots/admin-settings.jpg" width="260" alt="Admin settings page"/><br>
      <small>Settings</small>
    </td>
    <td align="center">
      <img src="docs/screenshots/admin-audit.jpg" width="260" alt="Admin audit page"/><br>
      <small>Audit</small>
    </td>
  </tr>
  <tr>
    <td align="center">
      <img src="docs/screenshots/admin-mobile-menu.jpg" width="150" alt="Admin mobile menu"/><br>
      <small>Mobile Menu</small>
    </td>
  </tr>
</table>

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
