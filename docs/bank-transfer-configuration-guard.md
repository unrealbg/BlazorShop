# Bank Transfer configuration guard

Implements [Issue #130](https://github.com/unrealbg/BlazorShop/issues/130), following R02/E08 in [the RC readiness audit](https://github.com/unrealbg/BlazorShop/pull/129). Source baseline: `9abba4d642798aa1fabfe98fca72990022fb0d4a`. This addresses the optional bank method only; it does not grant release or deployment approval.

## Problem and implementation

The previous discovery service advertised Bank Transfer without checking merchant settings. `BuildBankTransferInfo` substituted a hardcoded destination when the IBAN was absent. New real HTTP/PostgreSQL negative tests reproduced HTTP 200 success for nine invalid configurations on the unchanged application baseline before the guard was implemented. The older audit independently recorded the same boundary without publishing destination details.

`BankTransferConfiguration` now supplies one deterministic validation/normalization implementation to payment-method discovery and the checkout executor. Each service obtains a validated settings snapshot from its existing `IOptions<BankTransferSettings>` dependency; no new startup validator, provider, hot-reload mechanism or configuration endpoint is introduced.

- IBAN, Beneficiary and BankName are required. AdditionalInfo is optional.
- IBAN is trimmed, ordinary grouping spaces removed, and letters uppercased. It must have 15–34 ASCII characters, two country letters, two check digits in 02–98, an alphanumeric BBAN that is not all zeroes, and a MOD-97 remainder of one after rearrangement/letter expansion. Hyphens, internal tabs and non-ASCII IBAN characters are rejected.
- Beneficiary and bank name are trimmed; blank/punctuation-only values and common normalized template markers are rejected. The explicit finite sentinel list includes `YOUR_BENEFICIARY`, `BANK_NAME`, `CHANGE_ME`, `N/A`, `TODO`, `placeholder`, `Test Bank` and equivalent punctuation/case forms. This is not a semantic merchant-identity verification service.
- Validation is **generic structure/checksum**, not a country registry or national BBAN-format validator. It does not establish that a country/bank participates in IBAN, that an account is routable, that the account exists or that the merchant owns it. Those checks remain an independent owner/merchant release gate. No banking lookup or real transfer is performed.
- Invalid configuration removes the bank method by its stable ID (including localized labels) and by its existing Bank Transfer label. COD, enabled Stripe and application startup have no valid-bank-settings prerequisite.
- API appsettings defaults contain no destination/merchant values. The hardcoded executor fallback is removed. No replacement account is shipped.

For a fresh owned checkout, a second guard inside `CheckoutOrchestrator` rejects the bank method even if method discovery incorrectly advertises it. The guard precedes cart resolution, success snapshot persistence, order creation, inventory reservation and provider/email calls. It uses the existing HTTP 400/non-success validation convention without returning settings or financial details. A durable **failed idempotency outcome** is retained; no successful pending outcome, Order, OrderLine, inventory mutation, payment transaction or bank email is created.

## Durable outcomes and recovery

| Situation | Result |
| --- | --- |
| Missing/invalid configuration, fresh bank key | Method hidden; HTTP 400; failed outcome only, no commerce effects |
| Identical retry of that failed key | Original HTTP 400 replay; no new effects |
| Configuration corrected, retry of old failed key | Original failure remains authoritative; a fresh key is required |
| Valid configuration, fresh bank checkout | Existing pending-payment outcome, immutable amount/currency/reference and reserved inventory |
| Completed valid bank outcome, configuration later removed | Exact persisted response replay, no new order/reservation/email |
| Committed bank order with valid pending outcome, completion interrupted, configuration later removed | Recover from stored instructions after lease acquisition and identity checks; no replacement reservation or current-setting substitution |
| Committed bank order, no outcome snapshot and no valid current settings | HTTP 409 non-success; release the lease, preserve the order/stock/state, return no instructions; explicit recovery is required |
| Unknown or disabled legacy PayPal method | Existing safe invalid-method failure; no commerce effects |
| COD or enabled Stripe with missing bank settings | Existing checkout behavior remains available |

Discovery exclusion must not prevent committed-bank recovery: only that existing bank-order path bypasses current bank discovery after a durable claim, with existing customer/reference identity checks retained. Terminal replay still occurs before execution. Neither completed outcomes nor historical order records are rewritten or retrospectively validated against new merchant settings.

Exact terminal replay does not resend email. The pre-existing interrupted-completion recovery behavior can send an email again when prior completion/delivery acknowledgement is uncertain; this PR does not claim exactly-once mail delivery or introduce an outbox. If a historical persisted outcome contains bad instructions, it is not silently corrected by this guard and needs separate owner handling before payment. No schema, inventory locking, order snapshots, Stripe lifecycle, authorization or session changes are made.

## Regression evidence

`BankTransferConfigurationHttpTests` contains **19 mandatory cases**, using the real application/controller pipeline, bearer login/Identity, migrated disposable PostgreSQL and real checkout/inventory/idempotency services. Email is captured through a strict boundary double; the positive Stripe control uses a synthetic provider response, not a live payment. These are not SMTP/provider-delivery tests.

The cases cover nine invalid configurations, safe failure replay, valid normalized/plain settings, optional AdditionalInfo, removal after completion, committed recovery with/without a snapshot, stale discovery, correction with a new key, unknown/legacy methods and COD/Stripe independence. Assertions inspect persisted orders/lines, reservations, stock, payment transactions and outcomes; bank email calls are counted without logging destination values. A focused unit test separately proves that the independent guard never invokes pending-success persistence or commerce/provider/email calls.

Existing bank fixtures used invalid dummy check digits. They now generate a checksum-valid synthetic account with a synthetic bank identifier and random numeric suffix; no usable merchant destination is embedded. The helper is test-only and shared with the browser fixture. Contract-serialization fixtures remain dummy data because they do not execute checkout validation.

CI retains all existing gates and 40 mandatory Chromium scenarios twice plus 11 targeted scenarios each on Firefox/WebKit. The PostgreSQL 16 filter additionally includes this HTTP group and the full `CheckoutIdempotencyPostgreSqlTests` class; the strict TRX verifier requires all 19 bank cases on both database versions, in addition to the existing security/catalog groups. No gate, expected scenario or audit threshold is lowered.

Reproduce locally with a local disposable-container runtime (never a production database):

```powershell
dotnet build BlazorShop.sln -c Release -warnaserror
dotnet test BlazorShop.Tests/BlazorShop.Tests.csproj -c Release --no-build --filter FullyQualifiedName~BankTransferConfigurationHttpTests
# Repeat against PostgreSQL 16:
$env:BLAZORSHOP_TEST_POSTGRES_IMAGE = 'postgres:16.13-alpine3.23'
dotnet test BlazorShop.Tests/BlazorShop.Tests.csproj -c Release --no-build --filter FullyQualifiedName~BankTransferConfigurationHttpTests
Remove-Item Env:BLAZORSHOP_TEST_POSTGRES_IMAGE
```

Local pre-PR validation: focused service/HTTP tests **63/63**; Release build **0 warnings/errors**; full suite on **both PostgreSQL 16 and 17: 1007 passed, 10 existing opt-in live-smoke skips per version**. Final PR-head SHA, original CI/TRX/audit artifacts, containers and browser results are recorded in the PR validation ledger after CI completes. Skipped opt-in cases are not counted as passes. This source document avoids a self-referential commit hash.

## Remaining limits and references

This guard does not resolve SMTP durability/outbox work, immediate JWT revocation, C03/proxy attribution, routing topology, staging/recovery gaps or other RC audit findings. Merchant identity/account ownership, legal content and final payment/provider installation still require separate approval and evidence. Production remains NO-GO.

Framework guidance consulted: [ASP.NET Core 10 integration tests](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0), [EF Core transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions). Existing persistence/transaction boundaries are retained. Country-specific IBAN definitions are maintained separately in the [SWIFT IBAN Registry](https://www.swift.com/standards/standards-resources?category%5B0%5D=169031); this guard does not claim to implement that registry.
