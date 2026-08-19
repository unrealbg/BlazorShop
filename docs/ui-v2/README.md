# BlazorShop v2 UI Template

This directory is the visual source of truth for the BlazorShop v2 redesign.

The prototype is intentionally static and backend-independent. It mirrors the current routed BlazorShop feature surface so Razor implementation can be split into focused issues without asking a coding agent to invent missing UI decisions.

## Art direction

- Clean premium storefront with white/slate surfaces, blue primary actions and subtle violet accents.
- Dark navy admin shell with a light content canvas.
- Strong spacing, readable typography, restrained shadows, rounded cards and explicit commerce states.
- Responsive behavior is defined by the shared stylesheets.

## Review entry point

Open `prototype.html` first. It is the complete route-level prototype map. Open `components.html` for shared primitives, loading/empty/error states and router-state references.

## Route coverage

### Public storefront & commerce
- `index.html` — storefront `/`
- `catalog.html` — general catalog/filter experience
- `search-results.html` — `/search-result/{Filter}`
- `category.html` — `/category/{Slug}`
- `product.html` — public product detail
- `new-releases.html` — `/new-releases`
- `todays-deals.html` — `/todays-deals`
- `cart.html` — public cart
- `checkout.html` — authenticated `/account/checkout`
- `order-success.html` — `/payment-success` and bank-transfer/success presentation state
- `payment-cancel.html` — `/payment-cancel`
- `about.html` — `/about-us`
- `faq.html` — `/faq`
- `customer-service.html` — `/customer-service`
- `privacy.html` — `/privacy`
- `terms.html` — `/terms`
- `not-found.html` — storefront wildcard `/{*Path:nonfile}` / HTTP 404

### Account & identity
- `workspace-entry.html` — Web workspace `/` access-routing surface
- `login.html` — `/authentication/login` and login catch-all handoff
- `register.html` — `/authentication/register`
- `email-confirmation.html` — `/confirm-email`
- `account.html` — `/account`
- `account-orders.html` — `/account/orders`
- `account-order.html` — `/account/orders/{Id:guid}` detail state
- `account-profile.html` — `/account/profile`
- `account-notifications.html` — `/account/notifications`
- `account-settings.html` — `/account/settings`

`/authentication/logout` is intentionally not represented by a separate screen because the current route performs the logout operation and redirects to sign-in.

### Admin
- `admin-dashboard.html` — dashboard and overview metrics
- `admin-products.html` — products, filters, table and editor patterns
- `admin-categories.html` — category CRUD and category-level SEO
- `admin-orders.html` — payment/fulfillment/order state management
- `admin-users.html` — user search, roles, lock/email/password/deactivation operations
- `admin-inventory.html` — product and variant stock operations
- `admin-seo.html` — global SEO and organization metadata
- `admin-redirects.html` — redirect rule governance
- `admin-settings.html` — Store, Orders, Notifications and System settings
- `admin-audit.html` — audit search, table and metadata inspection

The admin prototype mirrors the complete current admin route surface. SEO and Redirects remain separate review screens even where production uses one Razor component for both routes.

### Shared/router states
- `components.html` — components plus 403/NotAuthorized, 404, 503/service-unavailable and generic error states
- `styles.css` — base responsive tokens and components
- `polish.css` — refined storefront/admin visual layer
- `consistency.css` — storefront header consistency layer

`NotAuthorized.razor` and the Web `NotFound.razor` are router/error states, not independent product routes; their visual treatment is therefore defined in `components.html` rather than duplicated as standalone route screens.

## Functional mapping

Current BlazorShop routes, data contracts and business rules remain authoritative during implementation. The prototype is a visual contract, not permission to change business behavior.

Planned capabilities such as Wishlist and Reviews are references only and must not become live features before their implementation issues are completed. Rating stars in static mockups are decorative placeholders for future Reviews work.

Commerce screens anticipate the v2 Commerce Core work (#88-#95): variant-aware cart lines, immutable order snapshots, inventory reservation, server-authoritative checkout, idempotency and explicit payment/order/fulfillment states.

Shipping/billing address UI is forward-compatible visual guidance only until corresponding domain capability exists. Inventory implementation must follow the authoritative model selected by #90 rather than preserve competing product/variant stock sources.

## Implementation rule

After approval, implementation issues should reference the exact HTML screen plus `components.html`; avoid subjective instructions such as “make it modern”. Preserve existing routes/contracts unless the linked implementation issue explicitly changes them.
