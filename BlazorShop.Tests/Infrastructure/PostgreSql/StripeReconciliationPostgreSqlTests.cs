namespace BlazorShop.Tests.Infrastructure.PostgreSql
{
    using BlazorShop.Application.Options;
    using BlazorShop.Domain.Entities;
    using BlazorShop.Domain.Entities.Payment;
    using BlazorShop.Infrastructure.Services;

    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Diagnostics;
    using Microsoft.Extensions.Logging;

    using Moq;

    using Xunit;

    [Collection(PostgreSqlCollection.Name)]
    public sealed class StripeReconciliationPostgreSqlTests
    {
        private readonly PostgreSqlFixture _database;

        public StripeReconciliationPostgreSqlTests(PostgreSqlFixture database)
        {
            _database = database;
        }

        [Fact]
        public async Task ValidPaidEvent_AtomicallyPaysOrderConsumesReservationAndStoresEvent()
        {
            var seed = await SeedPendingPaymentAsync();

            var outcome = await CreateService().ReconcileAsync(CreateEvent(seed, "evt_paid"));

            Assert.Equal(StripePaymentReconciliationOutcome.Processed, outcome);
            await AssertStateAsync(
                seed,
                PaymentTransactionStatus.Paid,
                PaymentOrderStatus.Paid,
                InventoryReservationStatus.Consumed,
                expectedStock: 4,
                expectedEvents: 1);
        }

        [Fact]
        public async Task SequentialDuplicatePaidEvent_StoresOneEventAndAppliesOneTransition()
        {
            var seed = await SeedPendingPaymentAsync();
            var providerEvent = CreateEvent(seed, "evt_duplicate");
            var service = CreateService();

            var first = await service.ReconcileAsync(providerEvent);
            var duplicate = await service.ReconcileAsync(providerEvent);

            Assert.Equal(StripePaymentReconciliationOutcome.Processed, first);
            Assert.Equal(StripePaymentReconciliationOutcome.Duplicate, duplicate);
            await AssertStateAsync(
                seed,
                PaymentTransactionStatus.Paid,
                PaymentOrderStatus.Paid,
                InventoryReservationStatus.Consumed,
                expectedStock: 4,
                expectedEvents: 1);
        }

        [Fact]
        public async Task ConcurrentDuplicatePaidEvent_SeparateConnectionsApplyExactlyOnce()
        {
            var seed = await SeedPendingPaymentAsync();
            var providerEvent = CreateEvent(seed, "evt_concurrent_duplicate");
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task<StripePaymentReconciliationOutcome> RunAsync()
            {
                await start.Task;
                return await CreateService().ReconcileAsync(providerEvent);
            }

            var first = RunAsync();
            var second = RunAsync();
            start.SetResult();
            var outcomes = await Task.WhenAll(first, second);

            Assert.Contains(StripePaymentReconciliationOutcome.Processed, outcomes);
            Assert.Contains(StripePaymentReconciliationOutcome.Duplicate, outcomes);
            await AssertStateAsync(
                seed,
                PaymentTransactionStatus.Paid,
                PaymentOrderStatus.Paid,
                InventoryReservationStatus.Consumed,
                expectedStock: 4,
                expectedEvents: 1);
        }

        [Theory]
        [InlineData(999, "eur", "Stripe amount mismatch")]
        [InlineData(1000, "usd", "Stripe currency mismatch")]
        public async Task PaidMonetaryMismatch_IsDurablyRejectedWithoutCommerceEffects(
            long amountMinor,
            string currency,
            string reason)
        {
            var seed = await SeedPendingPaymentAsync();
            var providerEvent = CreateEvent(seed, $"evt_{currency}_{amountMinor}") with
            {
                AmountTotal = amountMinor,
                Currency = currency,
            };

            var outcome = await CreateService().ReconcileAsync(providerEvent);

            Assert.Equal(StripePaymentReconciliationOutcome.Rejected, outcome);
            await AssertRejectedAsync(seed, reason);
        }

        [Fact]
        public async Task ProviderSessionMismatch_IsRejected()
        {
            var seed = await SeedPendingPaymentAsync();

            var outcome = await CreateService().ReconcileAsync(
                CreateEvent(seed, "evt_session_mismatch") with { SessionId = "cs_wrong" });

            Assert.Equal(StripePaymentReconciliationOutcome.Rejected, outcome);
            await AssertRejectedAsync(seed, "SessionId");
        }

        [Fact]
        public async Task ProviderPaymentIntentMismatch_IsRejected()
        {
            var seed = await SeedPendingPaymentAsync();

            var outcome = await CreateService().ReconcileAsync(
                CreateEvent(seed, "evt_intent_mismatch") with { PaymentIntentId = "pi_wrong" });

            Assert.Equal(StripePaymentReconciliationOutcome.Rejected, outcome);
            await AssertRejectedAsync(seed, "PaymentIntentId");
        }

        [Fact]
        public async Task TransactionAndOrderMetadataMismatch_IsRejected()
        {
            var seed = await SeedPendingPaymentAsync();

            var outcome = await CreateService().ReconcileAsync(
                CreateEvent(seed, "evt_order_mismatch") with { OrderId = Guid.NewGuid() });

            Assert.Equal(StripePaymentReconciliationOutcome.Rejected, outcome);
            await AssertRejectedAsync(seed, "order_id");
        }

        [Fact]
        public async Task ValidFailure_FailsTransactionAndReleasesInventoryExactlyOnce()
        {
            var seed = await SeedPendingPaymentAsync();

            var outcome = await CreateService().ReconcileAsync(
                CreateEvent(seed, "evt_failed", "checkout.session.async_payment_failed", null));

            Assert.Equal(StripePaymentReconciliationOutcome.Processed, outcome);
            await AssertStateAsync(
                seed,
                PaymentTransactionStatus.Failed,
                PaymentOrderStatus.PaymentFailed,
                InventoryReservationStatus.Released,
                expectedStock: 5,
                expectedEvents: 1);
        }

        [Fact]
        public async Task ValidExpiry_CancelsTransactionAndReleasesInventoryExactlyOnce()
        {
            var seed = await SeedPendingPaymentAsync();

            var outcome = await CreateService().ReconcileAsync(
                CreateEvent(seed, "evt_expired", "checkout.session.expired", "unpaid"));

            Assert.Equal(StripePaymentReconciliationOutcome.Processed, outcome);
            await AssertStateAsync(
                seed,
                PaymentTransactionStatus.Cancelled,
                PaymentOrderStatus.Cancelled,
                InventoryReservationStatus.Released,
                expectedStock: 5,
                expectedEvents: 1);
        }

        [Theory]
        [InlineData(999, "eur", "Stripe amount mismatch")]
        [InlineData(1000, "usd", "Stripe currency mismatch")]
        public async Task FailureOrExpiryWithContradictoryMoney_IsRejectedWithoutRelease(
            long amountMinor,
            string currency,
            string reason)
        {
            var seed = await SeedPendingPaymentAsync();
            var providerEvent = CreateEvent(
                seed,
                $"evt_failure_{currency}_{amountMinor}",
                "checkout.session.async_payment_failed",
                null) with
            {
                AmountTotal = amountMinor,
                Currency = currency,
            };

            var outcome = await CreateService().ReconcileAsync(providerEvent);

            Assert.Equal(StripePaymentReconciliationOutcome.Rejected, outcome);
            await AssertRejectedAsync(seed, reason);
        }

        [Theory]
        [InlineData("checkout.session.async_payment_failed")]
        [InlineData("checkout.session.expired")]
        public async Task PaidThenLateFailureOrExpiry_DoesNotDowngradeOrRestoreStock(string lateEventType)
        {
            var seed = await SeedPendingPaymentAsync();
            var service = CreateService();
            Assert.Equal(
                StripePaymentReconciliationOutcome.Processed,
                await service.ReconcileAsync(CreateEvent(seed, "evt_paid_first")));

            var late = await service.ReconcileAsync(
                CreateEvent(seed, $"evt_late_{lateEventType}", lateEventType, null));

            Assert.Equal(StripePaymentReconciliationOutcome.Rejected, late);
            await AssertStateAsync(
                seed,
                PaymentTransactionStatus.Paid,
                PaymentOrderStatus.Paid,
                InventoryReservationStatus.Consumed,
                expectedStock: 4,
                expectedEvents: 2);
        }

        [Fact]
        public async Task DuplicateFailureEvent_RestoresStockOnlyOnce()
        {
            var seed = await SeedPendingPaymentAsync();
            var providerEvent = CreateEvent(
                seed,
                "evt_duplicate_failure",
                "checkout.session.async_payment_failed",
                null);
            var service = CreateService();

            Assert.Equal(StripePaymentReconciliationOutcome.Processed, await service.ReconcileAsync(providerEvent));
            Assert.Equal(StripePaymentReconciliationOutcome.Duplicate, await service.ReconcileAsync(providerEvent));

            await AssertStateAsync(
                seed,
                PaymentTransactionStatus.Failed,
                PaymentOrderStatus.PaymentFailed,
                InventoryReservationStatus.Released,
                expectedStock: 5,
                expectedEvents: 1);
        }

        [Fact]
        public async Task FailureAfterEventClaim_RollsBackClaimAndRetryProcessesSuccessfully()
        {
            var seed = await SeedPendingPaymentAsync();
            var providerEvent = CreateEvent(seed, "evt_rollback");
            var interceptor = new ThrowOnceSavingChangesInterceptor();
            var failingService = new StripePaymentReconciliationService(
                _database.CreateContextFactory(interceptor),
                Mock.Of<ILogger<StripePaymentReconciliationService>>());

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => failingService.ReconcileAsync(providerEvent));
            await using (var afterFailure = _database.CreateContext())
            {
                Assert.Empty(await afterFailure.PaymentProviderEvents.ToListAsync());
                Assert.Equal(PaymentTransactionStatus.Pending,
                    (await afterFailure.PaymentTransactions.FindAsync(seed.PaymentTransactionId))!.Status);
            }

            Assert.Equal(
                StripePaymentReconciliationOutcome.Processed,
                await CreateService().ReconcileAsync(providerEvent));
            await AssertStateAsync(
                seed,
                PaymentTransactionStatus.Paid,
                PaymentOrderStatus.Paid,
                InventoryReservationStatus.Consumed,
                expectedStock: 4,
                expectedEvents: 1);
        }

        [Fact]
        public async Task UnknownTransaction_IsRejectedWithoutInventingCommerceState()
        {
            await _database.ResetDatabaseAsync();
            var unknownOrderId = Guid.NewGuid();
            var providerEvent = new StripeWebhookEventData(
                "evt_unknown",
                "checkout.session.completed",
                DateTime.UtcNow,
                "cs_unknown",
                "pi_unknown",
                unknownOrderId,
                unknownOrderId.ToString("D"),
                Guid.NewGuid(),
                "paid",
                1000,
                "eur",
                "complete");

            var outcome = await CreateService().ReconcileAsync(providerEvent);

            Assert.Equal(StripePaymentReconciliationOutcome.Rejected, outcome);
            await using var context = _database.CreateContext();
            Assert.Empty(await context.Orders.ToListAsync());
            Assert.Empty(await context.PaymentTransactions.ToListAsync());
            var ledger = Assert.Single(await context.PaymentProviderEvents.ToListAsync());
            Assert.Equal(PaymentProviderEventOutcome.Rejected, ledger.ProcessingOutcome);
        }

        [Fact]
        public async Task AmbiguousInitializationWithoutSession_MatchingWebhookBindsSessionExactlyOnce()
        {
            var seed = await SeedPendingPaymentAsync(providerSessionId: null, providerPaymentIntentId: null);

            var outcome = await CreateService().ReconcileAsync(CreateEvent(seed, "evt_bind"));

            Assert.Equal(StripePaymentReconciliationOutcome.Processed, outcome);
            await using var context = _database.CreateContext();
            var transaction = await context.PaymentTransactions.FindAsync(seed.PaymentTransactionId);
            Assert.Equal("cs_test", transaction!.ProviderSessionId);
            Assert.Equal("pi_test", transaction.ProviderPaymentIntentId);
        }

        [Fact]
        public async Task ConflictingConcurrentSessionBinding_OnlyOneSessionCanWin()
        {
            var seed = await SeedPendingPaymentAsync(providerSessionId: null, providerPaymentIntentId: null);
            var firstEvent = CreateEvent(seed, "evt_bind_a") with
            {
                SessionId = "cs_a",
                PaymentIntentId = "pi_a",
            };
            var secondEvent = CreateEvent(seed, "evt_bind_b") with
            {
                SessionId = "cs_b",
                PaymentIntentId = "pi_b",
            };
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task<StripePaymentReconciliationOutcome> RunAsync(StripeWebhookEventData providerEvent)
            {
                await start.Task;
                return await CreateService().ReconcileAsync(providerEvent);
            }

            var first = RunAsync(firstEvent);
            var second = RunAsync(secondEvent);
            start.SetResult();
            var outcomes = await Task.WhenAll(first, second);

            Assert.Contains(StripePaymentReconciliationOutcome.Processed, outcomes);
            Assert.Contains(StripePaymentReconciliationOutcome.Rejected, outcomes);
            await using var context = _database.CreateContext();
            var transaction = await context.PaymentTransactions.FindAsync(seed.PaymentTransactionId);
            Assert.Contains(transaction!.ProviderSessionId, new[] { "cs_a", "cs_b" });
            Assert.Contains(transaction.ProviderPaymentIntentId, new[] { "pi_a", "pi_b" });
            Assert.Equal(PaymentTransactionStatus.Paid, transaction.Status);
            Assert.Equal(2, await context.PaymentProviderEvents.CountAsync());
        }

        [Fact]
        public async Task ConfigurationChangeAfterOrderCreation_DoesNotChangeHistoricalCurrencyReconciliation()
        {
            var seed = await SeedPendingPaymentAsync(currency: "EUR");
            var currentConfiguration = new CommerceOptions { Currency = "USD" };

            var outcome = await CreateService().ReconcileAsync(CreateEvent(seed, "evt_historical_currency"));

            Assert.Equal("USD", currentConfiguration.Currency);
            Assert.Equal(StripePaymentReconciliationOutcome.Processed, outcome);
            await AssertStateAsync(
                seed,
                PaymentTransactionStatus.Paid,
                PaymentOrderStatus.Paid,
                InventoryReservationStatus.Consumed,
                expectedStock: 4,
                expectedEvents: 1);
        }

        [Fact]
        public async Task NoPaymentRequired_NonZeroOrderIsRejected()
        {
            var seed = await SeedPendingPaymentAsync();

            var outcome = await CreateService().ReconcileAsync(
                CreateEvent(seed, "evt_no_payment_nonzero") with { PaymentStatus = "no_payment_required" });

            Assert.Equal(StripePaymentReconciliationOutcome.Rejected, outcome);
            await AssertRejectedAsync(seed, "no_payment_required");
        }

        [Fact]
        public async Task NoPaymentRequired_ZeroOrderCanBePaidWhenAllValuesReconcile()
        {
            var seed = await SeedPendingPaymentAsync(expectedAmountMinor: 0, totalAmount: 0m);
            var providerEvent = CreateEvent(seed, "evt_no_payment_zero") with
            {
                PaymentStatus = "no_payment_required",
                AmountTotal = 0,
            };

            var outcome = await CreateService().ReconcileAsync(providerEvent);

            Assert.Equal(StripePaymentReconciliationOutcome.Processed, outcome);
            await AssertStateAsync(
                seed,
                PaymentTransactionStatus.Paid,
                PaymentOrderStatus.Paid,
                InventoryReservationStatus.Consumed,
                expectedStock: 4,
                expectedEvents: 1);
        }

        private StripePaymentReconciliationService CreateService() => new(
            _database.CreateContextFactory(),
            Mock.Of<ILogger<StripePaymentReconciliationService>>());

        private async Task<PaymentSeed> SeedPendingPaymentAsync(
            string? providerSessionId = "cs_test",
            string? providerPaymentIntentId = "pi_test",
            string currency = "EUR",
            long expectedAmountMinor = 1000,
            decimal totalAmount = 10m)
        {
            await _database.ResetDatabaseAsync();
            await using var context = _database.CreateContext();
            var category = new Category { Id = Guid.NewGuid(), Name = "Stripe reconciliation" };
            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Reconciliation product",
                Price = totalAmount,
                Quantity = 4,
                CategoryId = category.Id,
            };
            var order = new Order
            {
                Id = Guid.NewGuid(),
                UserId = "stripe-customer",
                Status = PaymentOrderStatus.PendingPayment,
                Reference = $"STRIPE-{Guid.NewGuid():N}",
                TotalAmount = totalAmount,
                Currency = currency,
                Lines =
                [
                    new OrderLine
                    {
                        ProductId = product.Id,
                        ProductNameSnapshot = product.Name!,
                        Quantity = 1,
                        UnitPrice = totalAmount,
                        LineTotal = totalAmount,
                    },
                ],
            };
            var reservation = new InventoryReservation
            {
                OrderId = order.Id,
                ProductId = product.Id,
                Quantity = 1,
                Status = InventoryReservationStatus.Reserved,
            };
            var paymentTransaction = new PaymentTransaction
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                Provider = PaymentProviderNames.Stripe,
                ProviderSessionId = providerSessionId,
                ProviderPaymentIntentId = providerPaymentIntentId,
                ExpectedAmountMinor = expectedAmountMinor,
                Currency = currency,
                Status = PaymentTransactionStatus.Pending,
            };
            context.AddRange(category, product, order, reservation, paymentTransaction);
            await context.SaveChangesAsync();
            return new PaymentSeed(
                order.Id,
                paymentTransaction.Id,
                product.Id,
                expectedAmountMinor,
                currency);
        }

        private static StripeWebhookEventData CreateEvent(
            PaymentSeed seed,
            string eventId,
            string eventType = "checkout.session.completed",
            string? paymentStatus = "paid") => new(
                eventId,
                eventType,
                DateTime.UtcNow,
                "cs_test",
                "pi_test",
                seed.OrderId,
                seed.OrderId.ToString("D"),
                seed.PaymentTransactionId,
                paymentStatus,
                seed.ExpectedAmountMinor,
                seed.Currency.ToLowerInvariant(),
                "complete");

        private async Task AssertRejectedAsync(PaymentSeed seed, string reasonFragment)
        {
            await AssertStateAsync(
                seed,
                PaymentTransactionStatus.Pending,
                PaymentOrderStatus.PendingPayment,
                InventoryReservationStatus.Reserved,
                expectedStock: 4,
                expectedEvents: 1);
            await using var context = _database.CreateContext();
            var ledger = await context.PaymentProviderEvents.SingleAsync();
            Assert.Equal(PaymentProviderEventOutcome.Rejected, ledger.ProcessingOutcome);
            Assert.Contains(reasonFragment, ledger.FailureReason!, StringComparison.OrdinalIgnoreCase);
        }

        private async Task AssertStateAsync(
            PaymentSeed seed,
            PaymentTransactionStatus paymentStatus,
            string orderStatus,
            InventoryReservationStatus reservationStatus,
            int expectedStock,
            int expectedEvents)
        {
            await using var context = _database.CreateContext();
            Assert.Equal(paymentStatus,
                (await context.PaymentTransactions.FindAsync(seed.PaymentTransactionId))!.Status);
            Assert.Equal(orderStatus, (await context.Orders.FindAsync(seed.OrderId))!.Status);
            Assert.Equal(reservationStatus,
                (await context.InventoryReservations.SingleAsync()).Status);
            Assert.Equal(expectedStock, (await context.Products.FindAsync(seed.ProductId))!.Quantity);
            Assert.Equal(expectedEvents, await context.PaymentProviderEvents.CountAsync());
        }

        private sealed record PaymentSeed(
            Guid OrderId,
            Guid PaymentTransactionId,
            Guid ProductId,
            long ExpectedAmountMinor,
            string Currency);

        private sealed class ThrowOnceSavingChangesInterceptor : SaveChangesInterceptor
        {
            private int _remainingFailures = 1;

            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData,
                InterceptionResult<int> result,
                CancellationToken cancellationToken = default)
            {
                if (Interlocked.Exchange(ref _remainingFailures, 0) == 1)
                {
                    throw new InvalidOperationException("Injected failure after provider event claim.");
                }

                return base.SavingChangesAsync(eventData, result, cancellationToken);
            }
        }
    }
}
