namespace BlazorShop.Tests.Presentation.Storefront;

using Xunit;

public sealed class StorefrontCategoryProductV2MarkupTests
{
    private const string Storefront = "BlazorShop.Presentation/BlazorShop.Storefront/";

    [Fact]
    public void CategoryPage_UsesLiveCatalogAndPreservesSeoAndHttpStatus()
    {
        var page = Read(Storefront + "Pages/CategoryPage.razor");

        Assert.Contains("@page \"/category/{Slug}\"", page);
        Assert.Contains("bs-v2-category-intro", page);
        Assert.Contains("bs-v2-category-toolbar", page);
        Assert.Contains("Products=\"_categoryPage.Products\"", page);
        Assert.Contains("@_categoryPage.Products.Count items", page);
        Assert.Contains("SeoContentSection", page);
        Assert.Contains("SeoHead Metadata=\"_metadata\" StructuredData=\"_structuredData\"", page);
        Assert.Contains("StorefrontResponseHeaders.ApplyNotFound(HttpContext)", page);
        Assert.Contains("StorefrontResponseHeaders.ApplyServiceUnavailable(HttpContext)", page);
        Assert.Contains("ComposeCategoryPageAsync", page);
        Assert.Contains("ComposeNotFoundPageAsync", page);
        Assert.Contains("ComposeServiceUnavailablePageAsync", page);
        Assert.DoesNotContain("data-storefront-wishlist", page);
    }

    [Fact]
    public void ProductPage_KeepsVariantAwareCartAndSeoContracts()
    {
        var page = Read(Storefront + "Pages/ProductPage.razor");

        Assert.Contains("@page \"/product/{Slug}\"", page);
        Assert.Contains("bs-v2-detail-layout", page);
        Assert.Contains("bs-v2-detail-image", page);
        Assert.Contains("No product image available", page);
        Assert.Contains("id=\"purchase\"", page);
        Assert.Contains("id=\"product-variant-select\"", page);
        Assert.Contains("data-storefront-variant-select", page);
        Assert.Contains("data-size-value=\"@variant.SizeValue\"", page);
        Assert.Contains("data-unit-price=\"@VariantPriceValue(variant)\"", page);
        Assert.Contains("data-storefront-add-to-cart", page);
        Assert.Contains("data-product-id=\"@_product.Id\"", page);
        Assert.Contains("data-variant-select=", page);
        Assert.Contains("data-feedback-target=\"#product-cart-feedback\"", page);
        Assert.Contains("id=\"product-cart-feedback\"", page);
        Assert.Contains("ProductGrid Products=\"_relatedProducts\"", page);
        Assert.Contains("StorefrontResponseHeaders.ApplyNotFound(HttpContext)", page);
        Assert.Contains("StorefrontResponseHeaders.ApplyServiceUnavailable(HttpContext)", page);
        Assert.Contains("ComposeProductPageAsync", page);
        Assert.Contains("SeoContentSection", page);
        Assert.DoesNotContain("★★★★★", page);
    }

    [Fact]
    public void RouteStyles_AreScopedResponsiveAndKeyboardAccessible()
    {
        var css = Read(Storefront + "wwwroot/css/ui-v2-storefront.css");

        Assert.Contains(".bs-v2-category-intro", css);
        Assert.Contains(".bs-v2-detail-layout", css);
        Assert.Contains(".bs-v2-detail-image img", css);
        Assert.Contains(".bs-v2-detail-field select", css);
        Assert.Contains(".bs-v2-detail-buy-actions", css);
        Assert.Contains("scroll-margin-top: 150px", css);
        Assert.Contains(".bs-v2-shell :is(.bs-v2-category,.bs-v2-detail) :is(select,input):focus-visible", css);
        Assert.Contains("@media (max-width: 980px)", css);
        Assert.Contains("@media (max-width: 640px)", css);
    }

    private static string Read(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "BlazorShop.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Unable to locate BlazorShop.sln from the test output directory.");
        }

        return File.ReadAllText(Path.Combine(directory.FullName, relativePath));
    }
}
