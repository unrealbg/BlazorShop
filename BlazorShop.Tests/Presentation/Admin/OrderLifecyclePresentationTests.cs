namespace BlazorShop.Tests.Presentation.Admin
{
    using Xunit;

    public sealed class OrderLifecyclePresentationTests
    {
        [Fact]
        public void AdminOrders_ShowsIndependentStatesAndOnlyOffersForwardFulfillmentActions()
        {
            var page = ReadRepositoryFile(
                "BlazorShop.Presentation/BlazorShop.Web/Pages/Admin/Orders.razor");
            var dashboardPage = ReadRepositoryFile(
                "BlazorShop.Presentation/BlazorShop.Web/Pages/Admin/Dashboard.razor");
            var dashboardCode = ReadRepositoryFile(
                "BlazorShop.Presentation/BlazorShop.Web/Pages/Admin/Dashboard.razor.cs");

            Assert.Contains("Order: <b>@o.OrderStatus</b>", page);
            Assert.Contains("Payment: <b>@o.PaymentStatus</b>", page);
            Assert.Contains("Fulfillment: <b>@o.FulfillmentStatus</b>", page);
            Assert.Contains("GetAllowedFulfillmentStatuses(_editOrder)", page);
            Assert.Contains("\"NotStarted\" => [\"NotStarted\", \"Shipped\"]", page);
            Assert.Contains("\"Shipped\" => [\"Shipped\", \"InTransit\"]", page);
            Assert.Contains("\"InTransit\" => [\"InTransit\", \"OutForDelivery\", \"Delivered\"]", page);
            Assert.Contains("\"OutForDelivery\" => [\"OutForDelivery\", \"Delivered\"]", page);
            Assert.DoesNotContain("<option>Delivered</option>", page);
            Assert.Contains("GetAllowedFulfillmentStatuses(_editOrder)", dashboardPage);
            Assert.Contains("string.Equals(o.PaymentStatus, \"Paid\"", dashboardCode);
            Assert.DoesNotContain("_totalRevenue = _orders.Sum", dashboardCode);
            Assert.DoesNotContain("PendingShipment", dashboardPage);
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
