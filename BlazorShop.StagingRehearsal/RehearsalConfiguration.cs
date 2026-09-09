namespace BlazorShop.StagingRehearsal;

internal sealed record RehearsalConfiguration(
    Uri StorefrontBaseUrl,
    Uri WebBaseUrl,
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
    string UserEmail,
    string UserPassword,
    string ArtifactDirectory)
{
    private const string ExpectedMode = "attach-upgraded-database";
    private const string ExpectedTargetSha = "8370f817857648f475041228ea6eaf228a3b9e34";
    private const string ExpectedMarker = "37000000-0000-0000-0000-000000000001";

    public static RehearsalConfiguration Load()
    {
        var mode = Required("BLAZORSHOP_REHEARSAL_MODE");
        if (!string.Equals(mode, ExpectedMode, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"BLAZORSHOP_REHEARSAL_MODE must be exactly '{ExpectedMode}'; the attach-only suite never starts a disposable stack.");
        }

        var targetSha = Required("BLAZORSHOP_REHEARSAL_TARGET_SHA");
        if (!string.Equals(targetSha, ExpectedTargetSha, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"The attach-only suite requires target SHA {ExpectedTargetSha}.");
        }

        var marker = Required("BLAZORSHOP_REHEARSAL_MARKER");
        if (!string.Equals(marker, ExpectedMarker, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The upgraded-database rehearsal marker is missing or unexpected.");
        }

        var storefrontBaseUrl = LoopbackUri("BLAZORSHOP_REHEARSAL_STOREFRONT_URL");
        var webBaseUrl = LoopbackUri("BLAZORSHOP_REHEARSAL_WEB_URL");

        return new RehearsalConfiguration(
            storefrontBaseUrl,
            webBaseUrl,
            Guid.Parse(ExpectedMarker),
            "Rehearsal Footwear",
            "rehearsal-footwear",
            Guid.Parse("37000000-0000-0000-0000-000000000010"),
            "Rehearsal Canvas Bag",
            "rehearsal-canvas-bag",
            "Synthetic simple product created by the historical rehearsal fixture.",
            12.50m,
            Guid.Parse("37000000-0000-0000-0000-000000000020"),
            "Rehearsal Trail Shoe",
            "rehearsal-trail-shoe",
            Guid.Parse("37000000-0000-0000-0000-000000000021"),
            "10",
            "REHEARSAL-US10-BLUE",
            31.25m,
            Required("BLAZORSHOP_REHEARSAL_USER_EMAIL"),
            Required("BLAZORSHOP_REHEARSAL_USER_PASSWORD"),
            Path.GetFullPath(Required("BLAZORSHOP_REHEARSAL_ARTIFACTS_DIR")));
    }

    private static string Required(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Required rehearsal setting {name} is missing.")
            : value;
    }

    private static Uri LoopbackUri(string name)
    {
        var value = Required(name);
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || !uri.IsLoopback
            || uri.Scheme != Uri.UriSchemeHttp)
        {
            throw new InvalidOperationException($"{name} must be an absolute HTTP loopback URL.");
        }

        return uri;
    }
}
