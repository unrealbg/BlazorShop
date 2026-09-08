namespace BlazorShop.Domain.Entities.Payment
{
    public static class OrderLifecyclePolicy
    {
        public static bool CanFulfill(Order order, out string? reason)
        {
            ArgumentNullException.ThrowIfNull(order);

            if (order.OrderStatus is OrderStatus.Cancelled or OrderStatus.Completed)
            {
                reason = $"Orders in {order.OrderStatus} state cannot be fulfilled.";
                return false;
            }

            if (order.PaymentMethod != OrderPaymentMethod.CashOnDelivery
                && order.PaymentStatus != OrderPaymentStatus.Paid)
            {
                reason = $"Unpaid {order.PaymentMethod} orders cannot be fulfilled.";
                return false;
            }

            if (order.OrderStatus != OrderStatus.Confirmed)
            {
                reason = "Only confirmed orders can be fulfilled.";
                return false;
            }

            reason = null;
            return true;
        }

        public static bool CanTransitionFulfillment(
            FulfillmentStatus current,
            FulfillmentStatus target,
            out bool alreadyApplied)
        {
            alreadyApplied = current == target;
            if (alreadyApplied)
            {
                return true;
            }

            return (current, target) switch
            {
                (FulfillmentStatus.NotStarted, FulfillmentStatus.Shipped) => true,
                (FulfillmentStatus.Shipped, FulfillmentStatus.InTransit) => true,
                (FulfillmentStatus.InTransit, FulfillmentStatus.OutForDelivery) => true,
                (FulfillmentStatus.InTransit, FulfillmentStatus.Delivered) => true,
                (FulfillmentStatus.OutForDelivery, FulfillmentStatus.Delivered) => true,
                _ => false,
            };
        }

        public static CoordinatedPaymentTransition GetStripeTerminalTransition(
            Order order,
            PaymentTransactionStatus transactionStatus)
        {
            ArgumentNullException.ThrowIfNull(order);

            var paymentStatus = transactionStatus switch
            {
                PaymentTransactionStatus.Paid => OrderPaymentStatus.Paid,
                PaymentTransactionStatus.Failed => OrderPaymentStatus.Failed,
                PaymentTransactionStatus.Cancelled => OrderPaymentStatus.Cancelled,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(transactionStatus),
                    transactionStatus,
                    "A Stripe payment transition must be terminal."),
            };
            var reservationStatus = transactionStatus == PaymentTransactionStatus.Paid
                ? InventoryReservationStatus.Consumed
                : InventoryReservationStatus.Released;
            return new CoordinatedPaymentTransition(
                GetOrderStatusAfterPayment(order, paymentStatus),
                paymentStatus,
                reservationStatus);
        }

        public static OrderStatus GetOrderStatusAfterPayment(Order order, OrderPaymentStatus paymentStatus)
        {
            ArgumentNullException.ThrowIfNull(order);

            return paymentStatus switch
            {
                OrderPaymentStatus.Paid when order.FulfillmentStatus == FulfillmentStatus.Delivered =>
                    OrderStatus.Completed,
                OrderPaymentStatus.Paid => OrderStatus.Confirmed,
                OrderPaymentStatus.Failed or OrderPaymentStatus.Cancelled => OrderStatus.Cancelled,
                _ => order.OrderStatus,
            };
        }
    }

    public sealed record CoordinatedPaymentTransition(
        OrderStatus OrderStatus,
        OrderPaymentStatus PaymentStatus,
        InventoryReservationStatus ReservationStatus);
}
