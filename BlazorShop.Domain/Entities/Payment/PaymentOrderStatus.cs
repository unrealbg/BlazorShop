namespace BlazorShop.Domain.Entities.Payment
{
    public static class PaymentOrderStatus
    {
        public const string PendingPayment = "PendingPayment";

        public const string Paid = "Paid";

        public const string PaymentFailed = "PaymentFailed";

        public const string Cancelled = "Cancelled";
    }
}
