namespace BlazorShop.Domain.Entities.Payment
{
    public static class PaymentMethodIds
    {
        public static readonly Guid CreditCard = Guid.Parse("3604fc1d-cd6a-46ad-ace4-9b5f8e03f43b");

        public static readonly Guid CashOnDelivery = Guid.Parse("6f2c2a7e-9f9b-4a0d-9f7f-2a1b3c4d5e6f");

        public static readonly Guid BankTransfer = Guid.Parse("b2e5c1d4-7a9f-4d2c-8f1e-3a4b5c6d7e8f");
    }
}
