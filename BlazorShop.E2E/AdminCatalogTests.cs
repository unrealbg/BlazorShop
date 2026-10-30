namespace BlazorShop.E2E;

using BlazorShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;

public sealed partial class AdminOperationsTests
{
    [Fact]
    public async Task AdminProducts_UploadCrudVariantsAndSeo()
    {
        var data = await application.SeedScenarioAsync();
        await application.GrantScenarioAdminAsync(data);
        string? uploadName = null;
        var name = $"Admin product {data.CategorySlug}";
        try
        {
            await LoginAsync(data);
            await GoAsync("admin/products");
            await Button(Page.Locator("main"), "Add Product").ClickAsync();
            await Dialog.GetByLabel("Product Name", new() { Exact = true }).FillAsync(name);
            await Dialog.GetByLabel("Price", new() { Exact = true }).FillAsync("18.75");
            await Dialog.GetByLabel("Product Description").FillAsync("Synthetic admin workflow product.");
            await Dialog.GetByLabel("Category", new() { Exact = true }).SelectOptionAsync(data.CategoryId.ToString());
            var uploaded = Page.WaitForResponseAsync(r => r.Url.Contains("/api/upload/image", StringComparison.OrdinalIgnoreCase) && r.Request.Method == "POST");
            await Dialog.Locator("input[type=file]").SetInputFilesAsync(new FilePayload
            {
                Name = "synthetic-product.webp", MimeType = "image/webp",
                Buffer = await File.ReadAllBytesAsync(Path.Combine(RepositoryRoot(), "BlazorShop.Presentation", "BlazorShop.Web", "wwwroot", "images", "products", "metro-runner.webp")),
            });
            var upload = await uploaded;
            Assert.True(upload.Ok);
            var body = (await upload.JsonAsync())!.Value;
            uploadName = Path.GetFileName(body.GetProperty("url").GetString());
            await Expect(Button(Dialog, "Save & Continue")).ToBeEnabledAsync();
            await Page.WaitForFunctionAsync("() => document.querySelector('dialog[open] img')?.naturalWidth > 0");
            await CaptureWidthsAsync("product-create-upload");
            await Button(Dialog, "Save & Continue").ClickAsync();
            await Expect(Dialog.GetByRole(AriaRole.Heading)).ToHaveTextAsync("Add Variants");
            await Dialog.GetByLabel("SKU", new() { Exact = true }).FillAsync($"UI-{data.CategorySlug}");
            await Dialog.GetByLabel("Price (optional)").FillAsync("21.50");
            await Button(Dialog, "Add Variant").ClickAsync();
            await Expect(Dialog.GetByRole(AriaRole.Row).Filter(new() { HasText = $"UI-{data.CategorySlug}" })).ToBeVisibleAsync();
            await CaptureWidthsAsync("product-add-variant");
            await Button(Dialog, "Finish").ClickAsync();
            await Page.GetByLabel("Search products...").FillAsync(name);
            await Page.Locator("main h1").ClickAsync();
            await Expect(Button(Page.Locator("main"), "Collapse All")).ToBeVisibleAsync();
            await Row(name).GetByTitle("Edit", new() { Exact = true }).ClickAsync();
            await Dialog.GetByLabel("Product Name", new() { Exact = true }).FillAsync(name + " edited");
            var variantRow = Dialog.GetByRole(AriaRole.Row).Filter(new() { HasText = $"UI-{data.CategorySlug}" });
            await Button(variantRow, "Edit").ClickAsync();
            await Dialog.GetByLabel("Color (optional)").FillAsync("Blue");
            await Button(Dialog, "Update Variant").ClickAsync();
            await Expect(variantRow).ToContainTextAsync("Blue");
            await Button(Dialog, "Update").ClickAsync();
            await Expect(Dialog).ToHaveCountAsync(0);
            await Row(name + " edited").GetByTitle("Edit", new() { Exact = true }).ClickAsync();
            await Button(Dialog, "SEO").ClickAsync();
            await Dialog.GetByLabel("Slug", new() { Exact = true }).FillAsync($"admin-product-{data.CategorySlug}");
            await Dialog.GetByLabel("Meta Title", new() { Exact = true }).FillAsync("Synthetic product metadata");
            var productSeo = Page.WaitForResponseAsync(r => r.Request.Method == "PUT" && r.Url.Contains("/api/admin/products/", StringComparison.Ordinal) && r.Url.EndsWith("/seo", StringComparison.Ordinal));
            await Button(Dialog, "Save SEO").ClickAsync();
            Assert.Equal(200, (await productSeo).Status);
            await Expect(Button(Dialog, "Save SEO")).ToBeEnabledAsync();
            await application.WithAdminDatabaseAsync(async db => Assert.Equal("Synthetic product metadata", (await db.Products.SingleAsync(p => p.CategoryId == data.CategoryId && p.Name == name + " edited")).MetaTitle));
            await CaptureWidthsAsync("product-seo");
            await Button(Dialog, "Catalog").ClickAsync();
            await Button(Dialog.GetByRole(AriaRole.Row).Filter(new() { HasText = $"UI-{data.CategorySlug}" }), "Delete").ClickAsync();
            await Expect(Dialog.GetByRole(AriaRole.Row).Filter(new() { HasText = $"UI-{data.CategorySlug}" })).ToHaveCountAsync(0);
            await Button(Dialog, "Cancel").Last.ClickAsync();
            await Row(name + " edited").GetByTitle("Delete", new() { Exact = true }).ClickAsync();
            await CaptureWidthsAsync("product-delete-confirmation");
            await Button(Dialog, "Yes, Delete").ClickAsync();
            await Expect(Row(name)).ToHaveCountAsync(0);
        }
        finally
        {
            await application.RemoveScenarioAsync(data);
            if (uploadName is not null && Guid.TryParseExact(Path.GetFileNameWithoutExtension(uploadName), "N", out _))
            {
                File.Delete(Path.Combine(RepositoryRoot(), "BlazorShop.Presentation", "BlazorShop.API", "uploads", uploadName));
            }
        }
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "BlazorShop.Presentation"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("The browser-test repository root was not found.");
    }

    [Fact]
    public async Task AdminCategories_CrudSeoAndConfirmation()
    {
        var data = await application.SeedScenarioAsync();
        await application.GrantScenarioAdminAsync(data);
        var name = $"Admin category {Guid.NewGuid():N}";
        try
        {
            await LoginAsync(data);
            await GoAsync("admin/categories");
            await Button(Page.Locator("main"), "Add Category").ClickAsync();
            await Dialog.GetByLabel("Category Name").FillAsync(name);
            await Button(Dialog, "Save").ClickAsync();
            await Expect(Row(name)).ToBeVisibleAsync();
            await Row(name).GetByTitle("Edit", new() { Exact = true }).ClickAsync();
            await Dialog.GetByLabel("Category Name").FillAsync(name + " edited");
            await Button(Dialog, "Update").ClickAsync();
            await Expect(Dialog).ToHaveCountAsync(0);
            await Row(name).GetByTitle("Edit", new() { Exact = true }).ClickAsync();
            await Button(Dialog, "SEO").ClickAsync();
            await Dialog.GetByLabel("Slug", new() { Exact = true }).FillAsync($"admin-category-{Guid.NewGuid():N}");
            await Dialog.GetByLabel("Meta Title", new() { Exact = true }).FillAsync("Synthetic category metadata");
            var categorySeo = Page.WaitForResponseAsync(r => r.Request.Method == "PUT" && r.Url.Contains("/api/admin/categories/", StringComparison.Ordinal) && r.Url.EndsWith("/seo", StringComparison.Ordinal));
            await Button(Dialog, "Save SEO").ClickAsync();
            Assert.Equal(200, (await categorySeo).Status);
            await Expect(Button(Dialog, "Save SEO")).ToBeEnabledAsync();
            await application.WithAdminDatabaseAsync(async db => Assert.Equal("Synthetic category metadata", (await db.Categories.SingleAsync(c => c.Name == name + " edited")).MetaTitle));
            await CaptureWidthsAsync("category-seo");
            await Page.Keyboard.PressAsync("Escape");
            await Row(name).GetByTitle("Delete", new() { Exact = true }).ClickAsync();
            await Button(Dialog, "Cancel").ClickAsync();
            await Expect(Row(name)).ToBeVisibleAsync();
            await Row(name).GetByTitle("Delete", new() { Exact = true }).ClickAsync();
            await CaptureWidthsAsync("category-delete-confirmation");
            await Button(Dialog, "Yes, Delete").ClickAsync();
            await Expect(Row(name)).ToHaveCountAsync(0);
        }
        finally
        {
            await application.WithAdminDatabaseAsync(async db => { db.Categories.RemoveRange(await db.Categories.Where(c => c.Name != null && c.Name.StartsWith(name)).ToListAsync()); await db.SaveChangesAsync(); });
            await application.RemoveScenarioAsync(data);
        }
    }

    [Fact]
    public async Task AdminInventory_PaginationAuthorityAndConflict()
    {
        var data = await application.SeedScenarioAsync();
        await application.GrantScenarioAdminAsync(data);
        var prefix = $"Paging {data.CategorySlug}";
        await application.WithAdminDatabaseAsync(async db =>
        {
            for (var i = 0; i < 21; i++) db.Products.Add(new Product { Name = $"{prefix} {i:D2}", Slug = $"{data.CategorySlug}-{i}", CategoryId = data.CategoryId, Price = 1m, Quantity = i, CreatedOn = DateTime.UtcNow });
            await db.SaveChangesAsync();
        });
        try
        {
            await LoginAsync(data);
            await GoAsync("admin/inventory");
            await FilterInventoryAsync(prefix);
            await Expect(Page.Locator("main article")).ToHaveCountAsync(20);
            await Button(Page.Locator("main"), "Next").ClickAsync();
            await Expect(Page.Locator("main article")).ToHaveCountAsync(1);
            await Button(Page.Locator("main"), "Previous").ClickAsync();
            await Expect(Page.Locator("main article")).ToHaveCountAsync(20);
            await Page.GetByLabel("Low", new() { Exact = true }).CheckAsync();
            await Button(Page.Locator("main"), "Apply").ClickAsync();
            await Expect(Page.Locator("main article")).ToHaveCountAsync(5);
            await Page.GetByLabel("Out", new() { Exact = true }).CheckAsync();
            await Button(Page.Locator("main"), "Apply").ClickAsync();
            await Expect(Page.Locator("main article")).ToHaveCountAsync(1);
            await Button(Page.Locator("main"), "Clear").ClickAsync();
            await FilterInventoryAsync(data.SimpleProductName);
            await Button(Page.Locator("main"), "Edit product stock").ClickAsync();
            await Dialog.GetByLabel("Product quantity").FillAsync("25");
            await CaptureWidthsAsync("inventory-product-stock");
            await Button(Dialog, "Save").ClickAsync();
            await Expect(Dialog).ToHaveCountAsync(0);
            await Expect(Page.GetByText("Product: 25", new() { Exact = true })).ToBeVisibleAsync();
            await FilterInventoryAsync(data.VariantProductName);
            await Expect(Button(Page.Locator("main"), "Edit product stock")).ToBeDisabledAsync();
            await Button(Row(data.VariantSku), "Edit").ClickAsync();
            await application.WithAdminDatabaseAsync(async db => { (await db.ProductVariants.SingleAsync(v => v.Id == data.VariantId)).Stock = 21; await db.SaveChangesAsync(); });
            await Dialog.GetByLabel("Variant stock").FillAsync("30");
            var response = Page.WaitForResponseAsync(r => r.Request.Method == "PUT" && r.Url.Contains("/admin/inventory/variant/", StringComparison.Ordinal));
            await Button(Dialog, "Save").ClickAsync();
            Assert.Equal(409, (await response).Status);
            await Expect(Dialog.GetByRole(AriaRole.Alert)).ToContainTextAsync("Variant stock changed after it was loaded. Refresh inventory and try again.");
            await CaptureWidthsAsync("inventory-conflict");
            await application.WithAdminDatabaseAsync(async db => Assert.Equal(21, (await db.ProductVariants.SingleAsync(v => v.Id == data.VariantId)).Stock));
            await Button(Dialog, "Cancel").ClickAsync();
            await FilterInventoryAsync(data.VariantProductName);
            await Button(Row(data.VariantSku), "Edit").ClickAsync();
            await Expect(Dialog.GetByLabel("Variant stock")).ToHaveValueAsync("21");
            await Dialog.GetByLabel("Variant stock").FillAsync("24");
            await Button(Dialog, "Save").ClickAsync();
            await Expect(Dialog).ToHaveCountAsync(0);
            await application.WithAdminDatabaseAsync(async db => Assert.Equal(24, (await db.ProductVariants.SingleAsync(v => v.Id == data.VariantId)).Stock));
        }
        finally { await application.RemoveScenarioAsync(data); }
    }
}
