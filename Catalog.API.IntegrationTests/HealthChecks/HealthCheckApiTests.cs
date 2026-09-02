using System.Net;

namespace Catalog.API.IntegrationTests.HealthChecks
{
    [Collection(IntegrationTestCollection.Name)]
    public class HealthCheckApiTests
    {
        private readonly HttpClient _client;

        public HealthCheckApiTests(ApiTestFixture fixture)
        {
            _client = fixture.CreateClient();
        }

        [Theory]
        [InlineData("/health")]
        [InlineData("/health/live")]
        [InlineData("/health/ready")]
        public async Task HealthEndpoint_Returns200(string path)
        {
            var response = await _client.GetAsync(path);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task ReadinessCheck_ReportsAllDependenciesHealthy()
        {
            var response = await _client.GetAsync("/health/ready");
            var body = await response.Content.ReadAsStringAsync();

            Assert.Contains("\"sqlserver\"", body);
            Assert.Contains("\"redis\"", body);
            Assert.Contains("\"rabbitmq\"", body);
            Assert.DoesNotContain("Unhealthy", body);
        }
    }
}
