namespace BlazorShop.Application.Services.Payment
{
    using BlazorShop.Application.DTOs.Payment;

    public static class StripeCheckoutInitializationValidator
    {
        public static bool TryValidateAuthoritativeTotal(
            StripeCheckoutInitialization initialization,
            out string? errorMessage)
        {
            ArgumentNullException.ThrowIfNull(initialization);

            if (initialization.PaymentTransactionId == Guid.Empty
                || initialization.OrderId == Guid.Empty
                || initialization.ExpectedAmountMinor < 0
                || string.IsNullOrWhiteSpace(initialization.Currency)
                || initialization.Lines is null
                || initialization.Lines.Count == 0
                || initialization.Lines.Any(line =>
                    line.Quantity <= 0
                    || line.UnitAmount < 0
                    || !string.Equals(
                        line.Currency,
                        initialization.Currency,
                        StringComparison.OrdinalIgnoreCase)))
            {
                errorMessage = "The card payment line snapshot is incomplete or inconsistent with its currency.";
                return false;
            }

            try
            {
                var lineTotal = initialization.Lines.Aggregate(
                    0L,
                    (total, line) => checked(total + checked(line.UnitAmount * line.Quantity)));
                if (lineTotal != initialization.ExpectedAmountMinor)
                {
                    errorMessage = "The card payment line total does not match the immutable order total.";
                    return false;
                }
            }
            catch (OverflowException)
            {
                errorMessage = "The card payment line total exceeds the supported amount range.";
                return false;
            }

            errorMessage = null;
            return true;
        }
    }
}
