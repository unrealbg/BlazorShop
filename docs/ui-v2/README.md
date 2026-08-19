# BlazorShop v2 UI Template

This directory is the visual source of truth for the BlazorShop v2 redesign.

The prototype is intentionally static and backend-independent. It mirrors the current BlazorShop feature surface so the real Razor implementation can be split into focused issues without asking a coding agent to invent missing UI decisions.

## Art direction

- Clean premium storefront inspired by the approved BlazorShop social-preview concept.
- White/slate public surfaces with blue primary actions and subtle violet accents.
- Dark navy admin shell with a light content canvas.
- Strong spacing, readable typography, restrained shadows, rounded cards and clear commerce states.
- Responsive behavior is defined in the shared stylesheet.

## Prototype screens

- `index.html` — storefront home
- `catalog.html` — catalog/category/search results
- `product.html` — product detail with variant and stock states
- `cart.html` — cart
- `checkout.html` — checkout/payment selection
- `account.html` — customer account and orders
- `admin-dashboard.html` — admin shell/dashboard
- `admin-products.html` — representative admin data table/filter/form patterns
- `components.html` — design-system reference and UI states

## Functional mapping

The template only surfaces capabilities that exist today or are already explicit roadmap items. Current BlazorShop routes, data contracts and business rules remain authoritative during implementation.

Planned-but-not-yet-implemented capabilities such as Wishlist and Reviews are shown only as optional/disabled design references where useful; they must not be exposed as live product features until their implementation issues are completed.

The commerce screens are designed to accommodate the v2 Commerce Core work (#88-#95): variant-aware cart lines, immutable order snapshots, inventory reservation, server-authoritative checkout, idempotency and explicit payment/order/fulfillment states.

## Implementation rule

When the template is approved, implementation issues should reference the relevant HTML screen plus `components.html` rather than using subjective instructions such as “make it modern”.
