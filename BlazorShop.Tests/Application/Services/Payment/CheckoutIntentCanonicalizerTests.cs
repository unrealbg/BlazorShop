namespace BlazorShop.Tests.Application.Services.Payment
{
    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Application.Services.Payment;
    using BlazorShop.Domain.Contracts.Payment;

    using Xunit;

    public sealed class CheckoutIntentCanonicalizerTests
    {
        [Fact]
        public void ReorderedAndDuplicateLines_ProduceSameFingerprintAndGroupedIntent()
        {
            var productA = Guid.NewGuid();
            var productB = Guid.NewGuid();
            var paymentMethod = Guid.NewGuid();
            var split = CheckoutIntentCanonicalizer.Canonicalize(new Checkout
            {
                PaymentMethodId = paymentMethod,
                Carts =
                [
                    new CartLineRequest(productB, null, 2),
                    new CartLineRequest(productA, null, 1),
                    new CartLineRequest(productA, null, 2),
                ],
            });
            var grouped = CheckoutIntentCanonicalizer.Canonicalize(new Checkout
            {
                PaymentMethodId = paymentMethod,
                Carts =
                [
                    new CartLineRequest(productA, null, 3),
                    new CartLineRequest(productB, null, 2),
                ],
            });

            Assert.True(split.IsValid);
            Assert.Equal(split.Fingerprint, grouped.Fingerprint);
            Assert.Equal(2, split.Lines.Count);
            Assert.Equal(3, split.Lines.Single(line => line.ProductId == productA).Quantity);
        }

        [Fact]
        public void MaterialIntentChanges_ProduceDifferentFingerprints()
        {
            var product = Guid.NewGuid();
            var variant = Guid.NewGuid();
            var payment = Guid.NewGuid();
            var baseline = Fingerprint(payment, product, null, 1);

            Assert.NotEqual(baseline, Fingerprint(payment, product, null, 2));
            Assert.NotEqual(baseline, Fingerprint(payment, Guid.NewGuid(), null, 1));
            Assert.NotEqual(baseline, Fingerprint(payment, product, variant, 1));
            Assert.NotEqual(baseline, Fingerprint(Guid.NewGuid(), product, null, 1));
        }

        [Fact]
        public void InvalidOrOverflowingQuantities_AreRejectedBeforeHashing()
        {
            var product = Guid.NewGuid();
            var invalid = CheckoutIntentCanonicalizer.Canonicalize(new Checkout
            {
                PaymentMethodId = Guid.NewGuid(),
                Carts = [new CartLineRequest(product, null, 0)],
            });
            var overflow = CheckoutIntentCanonicalizer.Canonicalize(new Checkout
            {
                PaymentMethodId = Guid.NewGuid(),
                Carts =
                [
                    new CartLineRequest(product, null, int.MaxValue),
                    new CartLineRequest(product, null, 1),
                ],
            });

            Assert.False(invalid.IsValid);
            Assert.False(overflow.IsValid);
            Assert.Null(invalid.Fingerprint);
            Assert.Null(overflow.Fingerprint);
        }

        private static string Fingerprint(Guid payment, Guid product, Guid? variant, int quantity) =>
            CheckoutIntentCanonicalizer.Canonicalize(new Checkout
            {
                PaymentMethodId = payment,
                Carts = [new CartLineRequest(product, variant, quantity)],
            }).Fingerprint!;
    }
}
