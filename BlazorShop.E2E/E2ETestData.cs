namespace BlazorShop.E2E;

public sealed record E2ETestData(
    Guid CategoryId,
    string CategoryName,
    string CategorySlug,
    Guid SimpleProductId,
    string SimpleProductName,
    string SimpleProductSlug,
    string SimpleProductDescription,
    decimal SimpleProductPrice,
    Guid VariantProductId,
    string VariantProductName,
    string VariantProductSlug,
    Guid VariantId,
    string VariantSize,
    string VariantSku,
    decimal VariantPrice,
    string UserId,
    string UserEmail,
    string UserPassword);
