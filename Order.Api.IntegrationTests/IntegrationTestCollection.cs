namespace Order.Api.IntegrationTests
{
    [CollectionDefinition(Name)]
    public class IntegrationTestCollection : ICollectionFixture<OrderApiTestFixture>
    {
        public const string Name = "OrderIntegration";
    }
}
