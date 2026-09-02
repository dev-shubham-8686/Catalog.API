namespace Catalog.API.IntegrationTests
{
    [CollectionDefinition(Name)]
    public class IntegrationTestCollection : ICollectionFixture<ApiTestFixture>
    {
        public const string Name = "Integration";
    }
}
