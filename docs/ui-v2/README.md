# BlazorShop v2 UI Template

This directory is the visual source of truth for the BlazorShop v2 redesign.

The prototype is intentionally static and backend-independent. It mirrors the current BlazorShop feature surface so the real Razor implementation can be split into focused issues without asking a coding agent to invent missing UI decisions.

## Art direction

- Clean premium storefront inspired by the approved BlazorShop social-preview concept.
- White/slate public surfaces with blue primary actions and subtle violet accents.
- Dark navy admin shell with a light content canvas.
- Strong spacing, readable typography, restrained shadows, rounded cards and clear commerce states.
- Responsive behavior is defined in the shared stylesheet.

## Review entry point

Open `prototype.html` first. It links the complete prototype journey and the component library.

## Prototype screens

### Storefront
- `index.html` — storefront home
- `catalog.html` — catalog/category/search results
- `product.html` — product detail with variant and stock states
- `cart.html` — cart
- `checkout.html` — checkout/payment selection
- `order-success.html` — server-authoritative order confirmation

### Account & identity
- `login.html` — sign-in/authentication pattern
- `account.html` — customer account overview and recent orders
- `account-order.html` — customer order detail and lifecycle timeline

### Admin
- `admin-dashboard.html` — dashboard and overview metrics
- `admin-products.html` — products, filters, table and editor patterns
- `admin-categories.html` — category CRUD and category-level SEO
- `admin-orders.html` — payment/fulfillment/order state management
- `admin-users.html` — user search, roles, lock/email/password/deactivation operations
- `admin-inventory.html` — product and variant stock operations
- `admin-seo.html` — global SEO and organization metadata
- `admin-redirects.html` — redirect rule governance for the `/admin/redirects` route
- `admin-settings.html` — Store, Orders, Notifications and System settings patterns
- `admin-audit.html` — audit search, table and detail/metadata inspection

The admin prototype intentionally mirrors the complete current admin route surface. `SEO` and `Redirects` are separate prototype screens even though production currently implements both routes in the same `Seo.razor` component.

### Shared
- `components.html` — design-system reference and UI states
- `styles.css` — base responsive visual tokens and component styles
- `polish.css` — refined storefront/admin visual layer
- `consistency.css` — storefront header consistency fixes used by public/customer screens

## Functional mapping

The template only surfaces capabilities that exist today or are already explicit roadmap items. Current BlazorShop routes, data contracts and business rules remain authoritative during implementation.

Planned-but-not-yet-implemented capabilities such as Wishlist and Reviews are visual references only; they must not be exposed as live product features until their implementation issues are completed. Any rating stars in the static mockups are therefore decorative placeholders for the future Reviews feature (#48), not an implementation requirement for the current storefront redesign.

The commerce screens are designed to accommodate the v2 Commerce Core work (#88-#95): variant-aware cart lines, immutable order snapshots, inventory reservation, server-authoritative checkout, idempotency and explicit payment/order/fulfillment states.

Shipping/billing address UI is included as a forward-compatible visual pattern, but it must not be wired into production until the corresponding commerce/domain capability exists.

The inventory prototype documents today's product/variant stock UI, but the production v2 implementation must follow the authoritative inventory model selected by #90 rather than preserve two competing stock sources.

## Implementation rule

When the template is approved, implementation issues should reference the relevant HTML screen plus `components.html` rather than using subjective instructions such as “make it modern”.

The prototype is a visual contract, not permission to change business behavior. Implementation PRs must preserve existing routes/contracts unless their linked issue explicitly changes them.
