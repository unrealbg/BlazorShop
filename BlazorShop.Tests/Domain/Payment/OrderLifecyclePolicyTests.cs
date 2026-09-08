namespace BlazorShop.Tests.Domain.Payment
{
    using BlazorShop.Domain.Entities.Payment;

    using Xunit;

    public sealed class OrderLifecyclePolicyTests
    {
        public static TheoryData<FulfillmentStatus, FulfillmentStatus, bool> FulfillmentTransitions => new()
        {
            { FulfillmentStatus.NotStarted, FulfillmentStatus.NotStarted, true },
            { FulfillmentStatus.NotStarted, FulfillmentStatus.Shipped, true },
            { FulfillmentStatus.NotStarted, FulfillmentStatus.Delivered, false },
            { FulfillmentStatus.Shipped, FulfillmentStatus.InTransit, true },
            { FulfillmentStatus.Shipped, FulfillmentStatus.Delivered, false },
            { FulfillmentStatus.InTransit, FulfillmentStatus.OutForDelivery, true },
            { FulfillmentStatus.InTransit, FulfillmentStatus.Delivered, true },
            { FulfillmentStatus.OutForDelivery, FulfillmentStatus.Delivered, true },
            { FulfillmentStatus.Delivered, FulfillmentStatus.Shipped, false },
        };

        [Theory]
        [MemberData(nameof(FulfillmentTransitions))]
        public void FulfillmentTransitionTable_AllowsOnlyDocumentedForwardEdges(
            FulfillmentStatus current,
            FulfillmentStatus target,
            bool expected)
        {
            Assert.Equal(
                expected,
                OrderLifecyclePolicy.CanTransitionFulfillment(current, target, out var alreadyApplied));
            Assert.Equal(current == target, alreadyApplied);
        }

        [Fact]
        public void CashOnDelivery_CanFulfillWhilePaymentIsPending()
        {
            var order = CreateOrder(OrderPaymentMethod.CashOnDelivery, OrderPaymentStatus.Pending);

            Assert.True(OrderLifecyclePolicy.CanFulfill(order, out var reason));
            Assert.Null(reason);
        }

        [Theory]
        [InlineData(OrderPaymentMethod.Stripe)]
        [InlineData(OrderPaymentMethod.BankTransfer)]
        public void NonCashOnDelivery_CannotFulfillBeforePayment(OrderPaymentMethod method)
        {
            var order = CreateOrder(method, OrderPaymentStatus.Pending);

            Assert.False(OrderLifecyclePolicy.CanFulfill(order, out var reason));
            Assert.Contains("Unpaid", reason);
        }

        [Fact]
        public void PaidConfirmedStripeOrder_CanFulfill()
        {
            var order = CreateOrder(OrderPaymentMethod.Stripe, OrderPaymentStatus.Paid);

            Assert.True(OrderLifecyclePolicy.CanFulfill(order, out var reason));
            Assert.Null(reason);
        }

        [Theory]
        [InlineData(OrderStatus.Cancelled)]
        [InlineData(OrderStatus.Completed)]
        public void TerminalOrder_CannotFulfill(OrderStatus status)
        {
            var order = CreateOrder(OrderPaymentMethod.Stripe, OrderPaymentStatus.Paid);
            order.OrderStatus = status;

            Assert.False(OrderLifecyclePolicy.CanFulfill(order, out var reason));
            Assert.Contains("cannot be fulfilled", reason);
        }

        [Theory]
        [InlineData(PaymentTransactionStatus.Paid, OrderPaymentStatus.Paid, InventoryReservationStatus.Consumed, OrderStatus.Confirmed)]
        [InlineData(PaymentTransactionStatus.Failed, OrderPaymentStatus.Failed, InventoryReservationStatus.Released, OrderStatus.Cancelled)]
        [InlineData(PaymentTransactionStatus.Cancelled, OrderPaymentStatus.Cancelled, InventoryReservationStatus.Released, OrderStatus.Cancelled)]
        public void StripeTerminalTransition_CoordinatesAllThreeLifecycleEffects(
            PaymentTransactionStatus transactionStatus,
            OrderPaymentStatus paymentStatus,
            InventoryReservationStatus reservationStatus,
            OrderStatus orderStatus)
        {
            var order = CreateOrder(OrderPaymentMethod.Stripe, OrderPaymentStatus.Pending);

            var transition = OrderLifecyclePolicy.GetStripeTerminalTransition(order, transactionStatus);

            Assert.Equal(paymentStatus, transition.PaymentStatus);
            Assert.Equal(reservationStatus, transition.ReservationStatus);
            Assert.Equal(orderStatus, transition.OrderStatus);
        }

        [Fact]
        public void PaidDeliveredOrder_CompletesWithoutChangingFulfillment()
        {
            var order = CreateOrder(OrderPaymentMethod.Stripe, OrderPaymentStatus.Pending);
            order.FulfillmentStatus = FulfillmentStatus.Delivered;

            var transition = OrderLifecyclePolicy.GetStripeTerminalTransition(
                order,
                PaymentTransactionStatus.Paid);

            Assert.Equal(OrderStatus.Completed, transition.OrderStatus);
            Assert.Equal(FulfillmentStatus.Delivered, order.FulfillmentStatus);
        }

        private static Order CreateOrder(OrderPaymentMethod method, OrderPaymentStatus paymentStatus) => new()
        {
            OrderStatus = OrderStatus.Confirmed,
            PaymentMethod = method,
            PaymentStatus = paymentStatus,
        };
    }
}
