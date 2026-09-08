namespace BlazorShop.Tests.Presentation.Payments
{
    using Xunit;

    public sealed class CheckoutPresentationTests
    {
        [Fact]
        public void Cart_ConsumesTypedCheckoutResultWithoutDisplayNameOrJsonProbing()
        {
            var cart = ReadRepositoryFile(
                "BlazorShop.Presentation/BlazorShop.Web/Pages/Payments/Cart.razor.cs");

            Assert.Contains("switch (result.PaymentKind)", cart);
            Assert.Contains("CheckoutPaymentKind.CashOnDelivery", cart);
            Assert.Contains("CheckoutPaymentKind.BankTransfer", cart);
            Assert.Contains("CheckoutPaymentKind.Stripe", cart);
            Assert.Contains("result.BankTransfer", cart);
            Assert.Contains("result.RedirectUrl", cart);
            Assert.DoesNotContain("TryGetProp", cart);
            Assert.DoesNotContain("paymentMethod.Name", cart);
            Assert.DoesNotContain("StartsWith(\"http", cart);
            Assert.DoesNotContain("JsonElement", cart);
            Assert.DoesNotContain("ConfirmOrder", cart);
        }

        [Fact]
        public void Cart_ClearsPresentationStateOnlyAfterTypedSuccessfulCheckout()
        {
            var cart = ReadRepositoryFile(
                "BlazorShop.Presentation/BlazorShop.Web/Pages/Payments/Cart.razor.cs");

            Assert.Contains("result.Success && result.Payload is not null", cart);
            Assert.Contains("await ClearCartAfterCheckoutAsync(checkout);", cart);
            Assert.Contains("CookieStorageService.RemoveAsync(Constant.Cart.Name)", cart);
            Assert.Contains("if (!cartCleanupSucceeded)", cart);
            Assert.Contains("CheckoutAttemptStore.ClearForIntentAsync(checkout)", cart);
            Assert.Contains("Your cart was not cleared", cart);
            var checkoutCleanup = cart.IndexOf(
                "private async Task ClearCartAfterCheckoutAsync(Checkout checkout)",
                StringComparison.Ordinal);
            var cookieRemoval = cart.IndexOf(
                "CookieStorageService.RemoveAsync(Constant.Cart.Name)",
                checkoutCleanup,
                StringComparison.Ordinal);
            var attemptFinalization = cart.IndexOf(
                "CheckoutAttemptStore.ClearForIntentAsync(checkout)",
                checkoutCleanup,
                StringComparison.Ordinal);
            Assert.True(checkoutCleanup >= 0);
            Assert.True(cookieRemoval > checkoutCleanup);
            Assert.True(attemptFinalization > cookieRemoval);
        }

        [Fact]
        public void Cart_GuardsQueuedPaymentClicksBeforeStartingAnotherCheckout()
        {
            var cart = ReadRepositoryFile(
                "BlazorShop.Presentation/BlazorShop.Web/Pages/Payments/Cart.razor.cs");

            Assert.Contains("paymentMethod is null || _processingMethodId.HasValue", cart);
        }

        [Fact]
        public void Cart_NavigatesRecoveredPaidStripeCheckoutToLocalSuccessPage()
        {
            var cart = ReadRepositoryFile(
                "BlazorShop.Presentation/BlazorShop.Web/Pages/Payments/Cart.razor.cs");

            Assert.Contains(
                "case CheckoutPaymentKind.Stripe when result.Status == CheckoutStatus.Confirmed:",
                cart);
            Assert.Contains("BuildSuccessPath(result, \"stripe\")", cart);
        }

        [Fact]
        public void SuccessPage_IsPresentationOnlyAndCannotCreateAnotherOrder()
        {
            var success = ReadRepositoryFile(
                "BlazorShop.Presentation/BlazorShop.Web/Pages/Payments/SuccessPayment.razor");

            Assert.Contains("Order received", success);
            Assert.Contains("Order ID", success);
            Assert.Contains("Order reference", success);
            Assert.DoesNotContain("CartService", success);
            Assert.DoesNotContain("ConfirmOrder", success);
            Assert.DoesNotContain("CookieStorageService", success);
            Assert.DoesNotContain("OnInitializedAsync", success);
            Assert.DoesNotContain("Checkout(", success);
        }

        [Fact]
        public void CancelPage_IsPresentationOnlyAndDoesNotMutateOrderOrInventory()
        {
            var cancel = ReadRepositoryFile(
                "BlazorShop.Presentation/BlazorShop.Web/Pages/Payments/CancelPayment.razor");

            Assert.Contains("does not change your order or inventory", cancel);
            Assert.DoesNotContain("CartService", cancel);
            Assert.DoesNotContain("Transition", cancel);
            Assert.DoesNotContain("OnInitialized", cancel);
        }

        [Fact]
        public void ConfirmOrderApiAndBrowserClientPaths_AreRemoved()
        {
            var controller = ReadRepositoryFile(
                "BlazorShop.Presentation/BlazorShop.API/Controllers/CartController.cs");
            var client = ReadRepositoryFile(
                "BlazorShop.Presentation/BlazorShop.Web.Shared/Services/CartService.cs");
            var routes = ReadRepositoryFile(
                "BlazorShop.Presentation/BlazorShop.Web.Shared/Constant.cs");

            Assert.DoesNotContain("confirm-order", controller);
            Assert.DoesNotContain("ConfirmOrder", controller);
            Assert.DoesNotContain("ConfirmOrder", client);
            Assert.DoesNotContain("ConfirmOrder", routes);
        }

        private static string ReadRepositoryFile(string relativePath)
        {
            return File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath));
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "BlazorShop.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException("Unable to locate BlazorShop.sln from the test output directory.");
        }
    }
}
