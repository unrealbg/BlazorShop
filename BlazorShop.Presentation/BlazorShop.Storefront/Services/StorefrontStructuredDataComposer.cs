namespace BlazorShop.Storefront.Services
{
    using System.Text.Json.Nodes;

    using BlazorShop.Application.DTOs.Seo;
    using BlazorShop.Storefront.Services.Contracts;
    using BlazorShop.Web.Shared.Models.Category;
    using BlazorShop.Web.Shared.Models.Product;

    public class StorefrontStructuredDataComposer : IStorefrontStructuredDataComposer
    {
        private readonly IStorefrontPublicUrlResolver _publicUrlResolver;
        private readonly IStorefrontSeoSettingsProvider _settingsProvider;

        public StorefrontStructuredDataComposer(
            IStorefrontPublicUrlResolver publicUrlResolver,
            IStorefrontSeoSettingsProvider settingsProvider)
        {
            _publicUrlResolver = publicUrlResolver;
            _settingsProvider = settingsProvider;
        }

        public async Task<StorefrontStructuredDataDocument> ComposeHomePageAsync(CancellationToken cancellationToken = default)
        {
            var context = await GetContextAsync(cancellationToken);
            if (context is null)
            {
                return StorefrontStructuredDataDocument.Empty;
            }

            return StorefrontStructuredDataDocument.CreateGraph(
            [
                CreateOrganizationNode(context),
                CreateWebSiteNode(context),
            ]);
        }

        public async Task<StorefrontStructuredDataDocument> ComposeWebPageAsync(string title, string relativePath, string? description, CancellationToken cancellationToken = default)
        {
            return await ComposePageAsync("WebPage", title, relativePath, description, cancellationToken);
        }

        public async Task<StorefrontStructuredDataDocument> ComposeCollectionPageAsync(string title, string relativePath, string? description, CancellationToken cancellationToken = default)
        {
            return await ComposePageAsync("CollectionPage", title, relativePath, description, cancellationToken);
        }

        public async Task<StorefrontStructuredDataDocument> ComposeFaqPageAsync(string title, string relativePath, string? description, IReadOnlyList<StorefrontFaqStructuredDataItem> faqItems, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(faqItems);

            if (faqItems.Count == 0)
            {
                return await ComposeWebPageAsync(title, relativePath, description, cancellationToken);
            }

            var context = await GetContextAsync(cancellationToken);
            var pageUrl = ResolvePageUrl(relativePath, context);
            if (context is null || string.IsNullOrWhiteSpace(pageUrl))
            {
                return StorefrontStructuredDataDocument.Empty;
            }

            var questions = new JsonArray();
            foreach (var faqItem in faqItems.Where(item => !string.IsNullOrWhiteSpace(item.Question) && !string.IsNullOrWhiteSpace(item.Answer)))
            {
                questions.Add(new JsonObject
                {
                    ["@type"] = "Question",
                    ["name"] = faqItem.Question,
                    ["acceptedAnswer"] = new JsonObject
                    {
                        ["@type"] = "Answer",
                        ["text"] = faqItem.Answer,
                    },
                });
            }

            if (questions.Count == 0)
            {
                return await ComposeWebPageAsync(title, relativePath, description, cancellationToken);
            }

            var faqPage = CreateWebPageLikeNode("FAQPage", context, pageUrl, title, description, "faqpage");
            faqPage["mainEntity"] = questions;

            return StorefrontStructuredDataDocument.CreateGraph([faqPage]);
        }

        public async Task<StorefrontStructuredDataDocument> ComposeCategoryPageAsync(GetCategory category, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(category);

            if (string.IsNullOrWhiteSpace(category.Slug))
            {
                return StorefrontStructuredDataDocument.Empty;
            }

            var context = await GetContextAsync(cancellationToken);
            var categoryUrl = ResolvePageUrl(StorefrontRoutes.Category(category.Slug), context);
            if (context is null || string.IsNullOrWhiteSpace(categoryUrl))
            {
                return StorefrontStructuredDataDocument.Empty;
            }

            var collectionPage = CreateWebPageLikeNode(
                "CollectionPage",
                context,
                categoryUrl,
                category.Name ?? "Category",
                FirstNonEmpty(category.MetaDescription, category.SeoContent),
                "collectionpage");

            var breadcrumb = CreateBreadcrumbNode(
                CreateNodeId(categoryUrl, "breadcrumb"),
                (context.BaseUrl, context.SiteName),
                (categoryUrl, category.Name ?? "Category"));

            return StorefrontStructuredDataDocument.CreateGraph([breadcrumb, collectionPage]);
        }

        public async Task<StorefrontStructuredDataDocument> ComposeProductPageAsync(GetProduct product, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(product);

            if (string.IsNullOrWhiteSpace(product.Slug))
            {
                return StorefrontStructuredDataDocument.Empty;
            }

            var context = await GetContextAsync(cancellationToken);
            var productUrl = ResolvePageUrl(StorefrontRoutes.Product(product.Slug), context);
            if (context is null || string.IsNullOrWhiteSpace(productUrl))
            {
                return StorefrontStructuredDataDocument.Empty;
            }

            var offer = new JsonObject
            {
                ["@type"] = "Offer",
                ["@id"] = CreateNodeId(productUrl, "offer"),
                ["price"] = JsonValue.Create(product.Price),
                ["url"] = productUrl,
            };

            var productNode = new JsonObject
            {
                ["@type"] = "Product",
                ["@id"] = CreateNodeId(productUrl, "product"),
                ["name"] = product.Name,
                ["url"] = productUrl,
                ["description"] = product.Description,
                ["offers"] = offer,
                ["mainEntityOfPage"] = productUrl,
            };

            AddString(productNode, "image", ResolveAssetUrl(product.Image, context));
            AddString(productNode, "category", product.Category?.Name);

            var breadcrumbItems = new List<(string Url, string Name)>
            {
                (context.BaseUrl, context.SiteName),
            };

            if (!string.IsNullOrWhiteSpace(product.Category?.Slug) && !string.IsNullOrWhiteSpace(product.Category?.Name))
            {
                breadcrumbItems.Add((ResolvePageUrl(StorefrontRoutes.Category(product.Category.Slug), context)!, product.Category.Name));
            }

            breadcrumbItems.Add((productUrl, product.Name ?? "Product"));

            var breadcrumb = CreateBreadcrumbNode(CreateNodeId(productUrl, "breadcrumb"), breadcrumbItems.ToArray());

            return StorefrontStructuredDataDocument.CreateGraph([breadcrumb, productNode]);
        }

        private async Task<StorefrontStructuredDataDocument> ComposePageAsync(string schemaType, string title, string relativePath, string? description, CancellationToken cancellationToken)
        {
            var context = await GetContextAsync(cancellationToken);
            var pageUrl = ResolvePageUrl(relativePath, context);
            if (context is null || string.IsNullOrWhiteSpace(pageUrl))
            {
                return StorefrontStructuredDataDocument.Empty;
            }

            return StorefrontStructuredDataDocument.CreateGraph([
                CreateWebPageLikeNode(schemaType, context, pageUrl, title, description, schemaType.ToLowerInvariant()),
            ]);
        }

        private async Task<StructuredDataContext?> GetContextAsync(CancellationToken cancellationToken)
        {
            var settings = await _settingsProvider.GetAsync(cancellationToken);
            var baseUrl = _publicUrlResolver.ResolveBaseUrl(settings?.BaseCanonicalUrl);
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return null;
            }

            var siteName = FirstNonEmpty(settings?.SiteName, settings?.CompanyName, "BlazorShop");
            var organizationName = FirstNonEmpty(settings?.CompanyName, settings?.SiteName, "BlazorShop");

            return new StructuredDataContext(
                baseUrl,
                siteName,
                organizationName,
                settings,
                CreateNodeId(baseUrl, "organization"),
                CreateNodeId(baseUrl, "website"));
        }

        private JsonObject CreateOrganizationNode(StructuredDataContext context)
        {
            var organization = new JsonObject
            {
                ["@type"] = "Organization",
                ["@id"] = context.OrganizationId,
                ["name"] = context.OrganizationName,
                ["url"] = context.BaseUrl,
            };

            AddString(organization, "logo", ResolveAssetUrl(context.Settings?.CompanyLogoUrl, context));
            AddString(organization, "email", context.Settings?.CompanyEmail);
            AddString(organization, "telephone", context.Settings?.CompanyPhone);

            var sameAs = CreateStringArray(
                ResolveAssetUrl(context.Settings?.FacebookUrl, context),
                ResolveAssetUrl(context.Settings?.InstagramUrl, context),
                ResolveAssetUrl(context.Settings?.XUrl, context));
            if (sameAs.Count > 0)
            {
                organization["sameAs"] = sameAs;
            }

            return organization;
        }

        private JsonObject CreateWebSiteNode(StructuredDataContext context)
        {
            return new JsonObject
            {
                ["@type"] = "WebSite",
                ["@id"] = context.WebSiteId,
                ["name"] = context.SiteName,
                ["url"] = context.BaseUrl,
                ["publisher"] = CreateReference(context.OrganizationId),
            };
        }

        private JsonObject CreateWebPageLikeNode(string schemaType, StructuredDataContext context, string pageUrl, string? title, string? description, string fragment)
        {
            var page = new JsonObject
            {
                ["@type"] = schemaType,
                ["@id"] = CreateNodeId(pageUrl, fragment),
                ["name"] = FirstNonEmpty(title, context.SiteName),
                ["url"] = pageUrl,
                ["isPartOf"] = CreateReference(context.WebSiteId),
            };

            AddString(page, "description", description);
            return page;
        }

        private static JsonObject CreateBreadcrumbNode(string breadcrumbId, params (string Url, string Name)[] items)
        {
            var listItems = new JsonArray();
            var position = 1;

            foreach (var item in items.Where(item => !string.IsNullOrWhiteSpace(item.Url) && !string.IsNullOrWhiteSpace(item.Name)))
            {
                listItems.Add(new JsonObject
                {
                    ["@type"] = "ListItem",
                    ["position"] = position++,
                    ["name"] = item.Name,
                    ["item"] = item.Url,
                });
            }

            return new JsonObject
            {
                ["@type"] = "BreadcrumbList",
                ["@id"] = breadcrumbId,
                ["itemListElement"] = listItems,
            };
        }

        private string? ResolvePageUrl(string? relativePath, StructuredDataContext? context)
        {
            return context is null
                ? null
                : _publicUrlResolver.ResolveAbsoluteUrl(relativePath, context.Settings?.BaseCanonicalUrl);
        }

        private string? ResolveAssetUrl(string? value, StructuredDataContext context)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : _publicUrlResolver.ResolveAbsoluteUrl(value, context.Settings?.BaseCanonicalUrl);
        }

        private static JsonArray CreateStringArray(params string?[] values)
        {
            var array = new JsonArray();
            foreach (var value in values.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                array.Add(value);
            }

            return array;
        }

        private static JsonObject CreateReference(string id)
        {
            return new JsonObject
            {
                ["@id"] = id,
            };
        }

        private static void AddString(JsonObject node, string propertyName, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                node[propertyName] = value;
            }
        }

        private static string CreateNodeId(string absoluteUrl, string fragment)
        {
            return $"{absoluteUrl.TrimEnd('#')}#{fragment}";
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }

        private sealed record StructuredDataContext(
            string BaseUrl,
            string SiteName,
            string OrganizationName,
            SeoSettingsDto? Settings,
            string OrganizationId,
            string WebSiteId);
    }
}