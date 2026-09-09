# BlazorShop

[![CI](https://github.com/unrealbg/BlazorShop/actions/workflows/ci.yml/badge.svg)](https://github.com/unrealbg/BlazorShop/actions/workflows/ci.yml)

BlazorShop is an open-source, opinionated .NET 10 e-commerce starter and reference application built with ASP.NET Core, Blazor, PostgreSQL, and Microsoft Aspire. It combines a server-rendered public storefront, a Blazor WebAssembly customer/admin workspace, an ASP.NET Core Web API, production Docker deployment, and an isolated live demo.

## Table of Contents
- [Introduction](#introduction)
- [Who Is It For?](#who-is-it-for)
- [Project Status](#project-status)
- [Features](#features)
- [Current Limitations](#current-limitations)
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
BlazorShop is designed as both a working shop application and a reusable reference for modern .NET commerce projects. It provides a server-rendered public storefront, a secure ASP.NET Core Web API backend, and a separate Blazor WebAssembly workspace for customer and administrator flows.

The repository includes catalog management, product variants, cart and checkout, Stripe/Cash on Delivery/Bank Transfer payment flows, order tracking, SEO tooling, an operational admin area, PostgreSQL persistence, Aspire orchestration, observability, CI, and production Docker deployment.

### Who Is It For?
- Developers looking for a practical .NET 10 / Blazor e-commerce starter or reference architecture.
- Small/medium businesses that want to bootstrap a .NET-based online shop and adapt it to their own requirements.
- Developers exploring ASP.NET Core, Blazor Web App, Blazor WebAssembly, EF Core, PostgreSQL, Aspire, and layered application architecture in a non-trivial project.

## Project Status
BlazorShop is functional and deployed as a live demo. The project is currently undergoing a focused commerce-core and production-hardening pass before treating the current architecture as a stronger reusable production starter.

Current high-priority work:
- #87 — secure administrator bootstrap
- #88 — preserve product variants through checkout and orders
- #89 — immutable order-line snapshots
- #90 — atomic inventory reservation and stock management
- #91 — authoritative server-side, order-first checkout
- #92 — checkout idempotency
- #93 — Stripe reconciliation and webhook idempotency
- #94 — separate order/payment/fulfillment lifecycle states

Additional production/UX work remains tracked in GitHub Issues. The issue tracker is treated as the current implementation backlog; README features describe what exists on `master`, not planned functionality.

## Features
- Authentication & Authorization
  - ASP.NET Core Identity, JWT access tokens, refresh-token flow
  - Email confirmation, password change, profile update
  - Role-based access (Admin/User)
  - Lockout and guarded administrative user-management flows
  - Isolated customer and administrator demo sessions with reset-on-logout/expiry behavior
- Catalog Management
  - Categories, products, product variants, SKU/size/stock data, image upload
  - Product search/typeahead UI
  - Admin inventory overview with low/out-of-stock filtering and product/variant stock updates
- Public SEO Storefront
  - Server-rendered product and category routes (`/product/{slug}` and `/category/{slug}`)
  - Published-only public catalog exposure and route-based metadata rendering
  - `/sitemap.xml` and `/robots.txt` for the published route surface
- Cart & Checkout
  - Persistent cart, quantity updates, totals
  - Multiple payment methods: Stripe (card), Cash on Delivery, Bank Transfer
  - Bank transfer instructions via email with order reference
- Orders & Tracking
  - One authoritative `Orders`/`OrderLines` persistence model for customer/admin history, with immutable purchase-time snapshots
  - Admin order management
  - Shipping status, carrier tracking number and tracking URL updates
- Newsletter
  - Email subscription with welcome email
- Admin Area
  - Dashboard, products, categories, variants, orders, users, inventory, SEO, redirects, settings, and audit log
  - User role editing, lock/unlock, email confirmation, password-change requirement flag, and guarded admin safety checks
  - Operational store/order/notification settings without exposing SMTP passwords or API secrets
  - Admin audit trail for sensitive catalog, SEO, redirect, order, user, settings, and inventory operations
- Developer Experience & Operations
  - OpenAPI/Swagger and Serilog logging
  - OpenTelemetry logging, metrics, and tracing with optional OTLP export
  - Microsoft Aspire AppHost orchestration and service discovery
  - Standard server-side HTTP resilience defaults and health checks
  - Automated unit/service/infrastructure tests and GitHub Actions CI
  - Modern UI with Tailwind-style classes, toast notifications, and Chart.js
  - Production Docker Compose deployment with separate Storefront, Web, API, and PostgreSQL services
  - Explicit deployment-only initial administrator bootstrap command
  - Configurable CORS, rate limiting, forwarded headers, HSTS/HTTPS behavior, and refresh-token cookie policy

## Current Limitations
These are known areas being actively hardened; see the linked issues for the source-of-truth acceptance criteria.

- **Variant/order integrity (#88, #89):** the commerce contracts and historical order snapshots are being strengthened so the exact purchased variant/SKU is authoritative throughout checkout and order history.
- **Inventory concurrency (#90):** atomic reservation/decrement behavior is still being implemented to prevent overselling under concurrent checkout.
- **Checkout lifecycle (#91, #92):** checkout is being moved to a fully authoritative server-side, order-first and idempotent flow.
- **Stripe reconciliation (#93):** signed Checkout Session events are durably deduplicated and reconciled against immutable local order, payment, provider-identity, amount, and currency state.
- **Legacy checkout archive (#95):** old checkout-history rows are retained losslessly as an operational archive that is deliberately outside runtime order history; see [the archive policy and cutover procedure](docs/legacy-checkout-archive.md).

## Technologies Used
- .NET 10, ASP.NET Core Web API
- Blazor Web App (server-rendered public storefront)
- Blazor WebAssembly (customer/admin workspace and existing interactive client)
- Entity Framework Core 10 + PostgreSQL
- ASP.NET Core Identity
- AutoMapper, FluentValidation
- Serilog
- OpenTelemetry
- Microsoft Aspire AppHost / ServiceDefaults
- Stripe integration
- Swashbuckle (Swagger/OpenAPI)
- xUnit, Moq and ASP.NET Core/Aspire testing infrastructure
- Docker / Docker Compose

## Requirements
- .NET 10 SDK compatible with the repository `global.json`
  - The repository currently pins SDK `10.0.107` with `rollForward: latestPatch`.
- Docker Desktop or another compatible container runtime (recommended for `BlazorShop.AppHost` and `compose.production.yml`)
- PostgreSQL if you run the API outside the AppHost-provisioned or Docker Compose database
- Modern WebAssembly-capable browser for the Web workspace
- Optional external configuration depending on enabled features:
  - Stripe Secret Key + Webhook Secret
  - SMTP credentials
  - Bank-transfer account details

## Getting Started
1) Clone the repository

   ```bash
   git clone https://github.com/unrealbg/BlazorShop.git
   cd BlazorShop
   ```

2) Configure the API

For local development, use `appsettings` overrides and preferably `dotnet user-secrets` for secrets.

- API configuration: `BlazorShop.Presentation/BlazorShop.API/appsettings.json`
- Production reference: `docs/production.appsettings.example.json`
- Storefront production reference: `docs/storefront.production.appsettings.example.json`

Core values commonly required:
- `ConnectionStrings:DefaultConnection`
- `Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience`
- `Stripe:Enabled`, `Stripe:SecretKey`, `Stripe:WebhookSecret`
- `Commerce:Currency` (required store currency; currently supports `EUR`, `GBP`, and `USD`)
- `BankTransfer:Iban`, `BankTransfer:Beneficiary`, `BankTransfer:BankName`, `BankTransfer:AdditionalInfo`
- `EmailSettings:From`, `DisplayName`, `SmtpServer`, `Port`, `UseSsl`, `Username`, `Password`

Production configuration also includes Identity confirmation requirements and runtime settings for CORS, forwarded headers, health endpoints, HSTS/HTTPS behavior, refresh-token cookies, and rate limiting. Use the production example/runbook rather than copying local-development defaults into production.

Tip: keep secrets out of source control via environment variables, deployment secrets, or `dotnet user-secrets` for local development.

3) Database

The API currently applies EF Core migrations automatically on startup.

You can also apply migrations manually from the solution root:

   ```bash
   dotnet ef database update --project BlazorShop.Infrastructure --startup-project BlazorShop.Presentation/BlazorShop.API
   ```

4) Bootstrap the initial administrator

Public registration always creates a normal `User` account. On a fresh deployment, supply `AdminBootstrap:Email`, `AdminBootstrap:Password`, and `AdminBootstrap:FullName` through environment variables, deployment secrets, or local user-secrets, then run the deployment-only command:

   ```bash
   dotnet run --project BlazorShop.Presentation/BlazorShop.API -- --bootstrap-admin
   ```

The command applies pending migrations, creates an email-confirmed `Admin`, and exits without starting the HTTP server. It refuses to run when an administrator or an account with the configured email already exists. Remove the bootstrap credentials from the active deployment configuration after the command succeeds. See `docs/production-runbook.md` for environment-variable and Docker Compose examples.

5) Run the app

Using the AppHost is the recommended local orchestration path:

   ```bash
   dotnet run --project BlazorShop.AppHost
   ```

Or run projects separately:

   ```bash
   dotnet run --project BlazorShop.Presentation/BlazorShop.API
   dotnet run --project BlazorShop.Presentation/BlazorShop.Storefront
   dotnet run --project BlazorShop.Presentation/BlazorShop.Web
   ```

Default dev URLs (may vary by environment):
- API: https://localhost:7094
- Storefront: ASP.NET Core Kestrel/AppHost-assigned URL
- Web: https://localhost:7258
- The Storefront and Web clients call the API at `https://localhost:7094/api/` by default unless overridden in configuration.

Runtime notes:
- Standalone Storefront serves its own static assets such as `/css/site.css` and `/favicon.svg`.
- Standalone and AppHost Storefront runs expose crawl documents at `/sitemap.xml` and `/robots.txt` for the published public route surface.
- With the API unavailable, static informational Storefront pages such as `/about-us`, `/privacy`, `/faq`, and `/terms` still return `200`, while catalog-backed routes such as `/`, `/new-releases`, `/todays-deals`, `/category/{slug}`, and `/product/{slug}` return `503`.
- With the API available, Storefront slug routes return `200` for published content and `404` for unknown slugs.
- AppHost is the easiest way to verify the full local stack because it runs API + Storefront + Web together and exposes the Aspire development experience.

5) Tests

   ```bash
   dotnet test BlazorShop.sln -c Release
   ```

The existing automated test suite covers application services, authentication, payment/cart behavior, repositories/infrastructure and migration/model consistency. Browser E2E coverage is tracked separately in #37.

## Project Structure
- **BlazorShop.Domain** – Core entities and contracts
- **BlazorShop.Application** – DTOs, application services, validations
- **BlazorShop.Infrastructure** – EF Core, repositories, Identity, email, payments, persistence/infrastructure services
- **BlazorShop.Presentation/BlazorShop.API** – ASP.NET Core Web API controllers and runtime configuration
- **BlazorShop.Presentation/BlazorShop.Storefront** – Server-rendered Blazor Web App public storefront
- **BlazorShop.Presentation/BlazorShop.Web** – Blazor WebAssembly customer/admin workspace and existing interactive client
- **BlazorShop.Presentation/BlazorShop.Web.Shared** – Shared Web client models/services
- **BlazorShop.AppHost** – Microsoft Aspire local orchestrator for API + Storefront + Web + PostgreSQL
- **BlazorShop.ServiceDefaults** – Shared Aspire defaults for telemetry, health checks, service discovery, and HTTP resilience
- **BlazorShop.Tests** – Automated unit/service/infrastructure tests

## API & Docs
- Swagger UI is available when the API runs in Development at `/swagger`.
- CORS is configuration-driven; loopback origins are allowed for Development while production origins are explicit.
- Health endpoints, rate limiting, forwarded headers, HSTS/HTTPS behavior and refresh-token cookie policy are configuration-driven.
- Production deployment references:
  - `docs/production-runbook.md`
  - `docs/production.appsettings.example.json`
  - `docs/storefront.production.appsettings.example.json`
  - `compose.production.yml`

## Screenshots
Captured from the live production demo on 4 August 2026. Source files live in
`docs/screenshots/`, with route and viewport metadata in
`docs/screenshots/manifest.json`.

### Public Storefront
<table>
  <tr>
    <td align="center">
      <img src="docs/screenshots/storefront-home.png" width="260" alt="Public storefront home page"/><br>
      <small>Storefront Home</small>
    </td>
    <td align="center">
      <img src="docs/screenshots/storefront-new-releases.png" width="260" alt="Public storefront new releases page"/><br>
      <small>New Releases</small>
    </td>
    <td align="center">
      <img src="docs/screenshots/storefront-todays-deals.png" width="260" alt="Public storefront today's deals page"/><br>
      <small>Today's Deals</small>
    </td>
  </tr>
  <tr>
    <td align="center">
      <img src="docs/screenshots/storefront-category-sneakers.png" width="260" alt="Public storefront category page"/><br>
      <small>Category</small>
    </td>
    <td align="center">
      <img src="docs/screenshots/storefront-product-metro-runner.png" width="260" alt="Public storefront product detail page"/><br>
      <small>Product Detail</small>
    </td>
    <td align="center">
      <img src="docs/screenshots/storefront-cart.png" width="260" alt="Public storefront cart page"/><br>
      <small>Cart</small>
    </td>
  </tr>
  <tr>
    <td align="center">
      <img src="docs/screenshots/storefront-about.png" width="260" alt="Public storefront about page"/><br>
      <small>About</small>
    </td>
    <td align="center">
      <img src="docs/screenshots/storefront-customer-service.png" width="260" alt="Public storefront customer service page"/><br>
      <small>Customer Service</small>
    </td>
    <td align="center">
      <img src="docs/screenshots/storefront-faq.png" width="260" alt="Public storefront FAQ page"/><br>
      <small>FAQ</small>
    </td>
  </tr>
  <tr>
    <td align="center">
      <img src="docs/screenshots/storefront-account-menu.png" width="260" alt="Public storefront account menu"/><br>
      <small>Account Menu</small>
    </td>
    <td align="center">
      <img src="docs/screenshots/storefront-mobile-menu.png" width="150" alt="Public storefront mobile menu"/><br>
      <small>Mobile Menu</small>
    </td>
  </tr>
</table>

### Account and Access
<table>
  <tr>
    <td align="center">
      <img src="docs/screenshots/web-workspace-entry.png" width="260" alt="Workspace access entry page"/><br>
      <small>Workspace Access</small>
    </td>
    <td align="center">
      <img src="docs/screenshots/auth-login.png" width="260" alt="Sign in page"/><br>
      <small>Sign In</small>
    </td>
    <td align="center">
      <img src="docs/screenshots/auth-register.png" width="260" alt="Register page"/><br>
      <small>Register</small>
    </td>
  </tr>
  <tr>
    <td align="center">
      <img src="docs/screenshots/account-dashboard.png" width="260" alt="Customer account dashboard"/><br>
      <small>Account Dashboard</small>
    </td>
    <td align="center">
      <img src="docs/screenshots/account-orders.png" width="260" alt="Customer account orders page"/><br>
      <small>Orders</small>
    </td>
    <td align="center">
      <img src="docs/screenshots/account-notifications.png" width="260" alt="Customer account notifications page"/><br>
      <small>Notifications</small>
    </td>
  </tr>
  <tr>
    <td align="center">
      <img src="docs/screenshots/account-profile.png" width="260" alt="Customer account profile page"/><br>
      <small>Profile</small>
    </td>
    <td align="center">
      <img src="docs/screenshots/account-settings.png" width="260" alt="Customer account settings page"/><br>
      <small>Settings</small>
    </td>
    <td align="center">
      <img src="docs/screenshots/account-checkout.png" width="260" alt="Customer account checkout page"/><br>
      <small>Checkout</small>
    </td>
  </tr>
  <tr>
    <td align="center">
      <img src="docs/screenshots/account-mobile-menu.png" width="150" alt="Customer account mobile menu"/><br>
      <small>Mobile Menu</small>
    </td>
  </tr>
</table>

### Admin Operations
<table>
  <tr>
    <td align="center">
      <img src="docs/screenshots/admin-dashboard.png" width="260" alt="Admin operations dashboard"/><br>
      <small>Dashboard</small>
    </td>
    <td align="center">
      <img src="docs/screenshots/admin-products.png" width="260" alt="Admin products page"/><br>
      <small>Products</small>
    </td>
    <td align="center">
      <img src="docs/screenshots/admin-product-add-modal.png" width="260" alt="Admin add product modal"/><br>
      <small>Add Product Modal</small>
    </td>
  </tr>
  <tr>
    <td align="center">
      <img src="docs/screenshots/admin-categories.png" width="260" alt="Admin categories page"/><br>
      <small>Categories</small>
    </td>
    <td align="center">
      <img src="docs/screenshots/admin-category-add-modal.png" width="260" alt="Admin add category modal"/><br>
      <small>Add Category Modal</small>
    </td>
    <td align="center">
      <img src="docs/screenshots/admin-inventory.png" width="260" alt="Admin inventory page"/><br>
      <small>Inventory</small>
    </td>
  </tr>
  <tr>
    <td align="center">
      <img src="docs/screenshots/admin-orders.png" width="260" alt="Admin orders page"/><br>
      <small>Orders</small>
    </td>
    <td align="center">
      <img src="docs/screenshots/admin-users.png" width="260" alt="Admin users page"/><br>
      <small>Users</small>
    </td>
    <td align="center">
      <img src="docs/screenshots/admin-seo.png" width="260" alt="Admin SEO page"/><br>
      <small>SEO</small>
    </td>
  </tr>
  <tr>
    <td align="center">
      <img src="docs/screenshots/admin-redirects.png" width="260" alt="Admin redirects page"/><br>
      <small>Redirects</small>
    </td>
    <td align="center">
      <img src="docs/screenshots/admin-settings.png" width="260" alt="Admin settings page"/><br>
      <small>Settings</small>
    </td>
    <td align="center">
      <img src="docs/screenshots/admin-audit.png" width="260" alt="Admin audit page"/><br>
      <small>Audit</small>
    </td>
  </tr>
  <tr>
    <td align="center">
      <img src="docs/screenshots/admin-mobile-menu.png" width="150" alt="Admin mobile menu"/><br>
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

When contributing against an existing issue, use its acceptance criteria as the implementation scope and keep unrelated refactors out of the same PR where possible.

## Demo

- Public storefront: https://shop.unrealbg.com
- Customer and admin workspace: https://account.unrealbg.com
- Customer demo: `demo.user@blazorshop.local`
- Administrator demo: `demo.admin@blazorshop.local`
- Shared demo password: `Demo123!`

Use the **Customer demo** or **Administrator demo** button on the sign-in page. Each button creates a private sandbox for that browser session. The sandbox starts with the published catalog and its own users, orders, settings, audit events, and uploads. Changes are visible in both the workspace and storefront for that session, but never reach the shared production tables.

Demo sessions use a secure cross-subdomain cookie and a session-bound JWT, expire after 30 minutes of inactivity, and are deleted immediately on sign-out. Temporary uploaded files are removed with the session. Card payments remain disabled in the public demo; the non-card checkout paths can be explored safely.

## License
MIT License. See the LICENSE file for details.

## Acknowledgements
- https://github.com/unrealbg – Creator and maintainer of BlazorShop.
