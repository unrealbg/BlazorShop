namespace BlazorShop.E2E;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class E2ECollection : ICollectionFixture<E2EApplicationFixture>
{
    public const string Name = "BlazorShop browser E2E";
}
