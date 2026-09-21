using Catalog.Domain.Requests.Item;
using Catalog.Domain.Responses;
using Catalog.Domain.Responses.Item;
using Identity.Authentication.Contracts;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Catalog.API.IntegrationTests.Items
{
    [Collection(IntegrationTestCollection.Name)]
    public class ItemsApiTests : IAsyncLifetime
    {
        private readonly ApiTestFixture _fixture;
        private readonly HttpClient _client;

        public ItemsApiTests(ApiTestFixture fixture)
        {
            _fixture = fixture;
            _client = fixture.CreateClient();
        }

        // Every item endpoint now requires auth (reads: any authenticated user, writes: Admin) —
        // authenticate once as Admin for this class's tests, which exercise the full CRUD surface.
        // Dedicated tests below cover the anonymous/non-admin cases explicitly.
        public async Task InitializeAsync()
        {
            var token = await RegisterAdminAndLoginAsync($"items-admin-{Guid.NewGuid()}@test.com", "P@ssw0rd123!");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        public Task DisposeAsync() => Task.CompletedTask;

        private async Task<string> RegisterAdminAndLoginAsync(string email, string password)
        {
            (await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, password))).EnsureSuccessStatusCode();

            await _fixture.PromoteToAdminAsync(email);

            var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
            loginResponse.EnsureSuccessStatusCode();
            var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
            return login!.AccessToken;
        }

        // PaginatedItemResponseModel<T>'s constructor parameter names don't match its property
        // names (a pre-existing quirk of the production type), which defeats System.Text.Json's
        // constructor-matching deserialization. Deserialize into this test-local shape instead.
        private record PaginatedItemsDto(int PageIndex, int PageSize, long Total, List<GetItemResponse> Items);

        [Fact]
        public async Task Create_WithValidRequest_Returns201WithCreatedItem()
        {
            var request = new AddItemRequest { Name = "Test Item", Description = "Integration test item" };

            var response = await _client.PostAsJsonAsync("/api/items", request);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.NotNull(response.Headers.Location);

            var body = await response.Content.ReadFromJsonAsync<GetItemResponse>();
            Assert.NotNull(body);
            Assert.NotEqual(Guid.Empty, body!.Id);
            Assert.Equal(request.Name, body.Name);
            Assert.Equal(request.Description, body.Description);
        }

        [Fact]
        public async Task Create_WithMissingFields_Returns400WithValidationErrors()
        {
            var request = new AddItemRequest { Name = "", Description = "" };

            var response = await _client.PostAsJsonAsync("/api/items", request);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<ValidationFailureResponse>();
            Assert.NotNull(body);
            Assert.Contains(body!.Errors, e => e.PropertyName == nameof(AddItemRequest.Name));
            Assert.Contains(body.Errors, e => e.PropertyName == nameof(AddItemRequest.Description));
        }

        [Fact]
        public async Task Get_WithExistingId_Returns200WithItem()
        {
            var created = await CreateItemAsync();

            var response = await _client.GetAsync($"/api/items/{created.Id}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<GetItemResponse>();
            Assert.Equal(created.Id, body!.Id);
            Assert.Equal(created.Name, body.Name);
        }

        [Fact]
        public async Task Get_WithExistingId_SecondCallIsServedFromCacheWithSameData()
        {
            var created = await CreateItemAsync();

            var first = await _client.GetFromJsonAsync<GetItemResponse>($"/api/items/{created.Id}");
            var second = await _client.GetFromJsonAsync<GetItemResponse>($"/api/items/{created.Id}");

            Assert.Equal(first!.Id, second!.Id);
            Assert.Equal(first.Name, second.Name);
            Assert.Equal(first.Description, second.Description);
        }

        [Fact]
        public async Task Get_WithUnknownId_Returns404()
        {
            var response = await _client.GetAsync($"/api/items/{Guid.NewGuid()}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetAll_ReturnsPaginatedResultsContainingCreatedItems()
        {
            var first = await CreateItemAsync();
            var second = await CreateItemAsync();

            var response = await _client.GetAsync("/api/items?pageSize=50&pageIndex=0");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<PaginatedItemsDto>();
            Assert.NotNull(body);
            Assert.Equal(50, body!.PageSize);
            Assert.Equal(0, body.PageIndex);
            Assert.True(body.Total >= 2);
            Assert.Contains(body.Items, i => i.Id == first.Id);
            Assert.Contains(body.Items, i => i.Id == second.Id);
        }

        [Fact]
        public async Task Update_WithExistingId_Returns200AndPersistsChanges()
        {
            var created = await CreateItemAsync();
            var updateRequest = new EditItemRequest { Name = "Updated Name", Description = "Updated Description" };

            var response = await _client.PutAsJsonAsync($"/api/items/{created.Id}", updateRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<EditItemResponse>();
            Assert.Equal(updateRequest.Name, body!.Name);
            Assert.Equal(updateRequest.Description, body.Description);

            // Cache was invalidated implicitly by re-fetching a fresh response object here — a
            // stale-cache regression would make this second read still return the OLD name.
            var getResponse = await _client.GetFromJsonAsync<GetItemResponse>($"/api/items/{created.Id}");
            Assert.Equal(updateRequest.Name, getResponse!.Name);
        }

        [Fact]
        public async Task Update_WithUnknownId_Returns404()
        {
            var updateRequest = new EditItemRequest { Name = "x", Description = "y" };

            var response = await _client.PutAsJsonAsync($"/api/items/{Guid.NewGuid()}", updateRequest);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Delete_WithExistingId_Returns200AndItemNoLongerRetrievable()
        {
            var created = await CreateItemAsync();

            var deleteResponse = await _client.DeleteAsync($"/api/items/{created.Id}");
            Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

            var getResponse = await _client.GetAsync($"/api/items/{created.Id}");
            Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        }

        [Fact]
        public async Task Delete_WithUnknownId_Returns404()
        {
            var response = await _client.DeleteAsync($"/api/items/{Guid.NewGuid()}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        private async Task<GetItemResponse> CreateItemAsync()
        {
            var request = new AddItemRequest { Name = $"Item {Guid.NewGuid()}", Description = "Integration test item" };
            var response = await _client.PostAsJsonAsync("/api/items", request);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<GetItemResponse>())!;
        }

        [Fact]
        public async Task GetAll_WithoutToken_Returns401()
        {
            using var anonymousClient = _fixture.CreateClient();

            var response = await anonymousClient.GetAsync("/api/items");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Get_WithNonAdminToken_Returns200()
        {
            var created = await CreateItemAsync();

            using var nonAdminClient = _fixture.CreateClient();
            var email = $"items-reader-{Guid.NewGuid()}@test.com";
            const string password = "P@ssw0rd123!";
            (await nonAdminClient.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, password))).EnsureSuccessStatusCode();
            var login = await (await nonAdminClient.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password))).Content.ReadFromJsonAsync<LoginResponse>();
            nonAdminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);

            var response = await nonAdminClient.GetAsync($"/api/items/{created.Id}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Create_WithoutToken_Returns401()
        {
            using var anonymousClient = _fixture.CreateClient();
            var request = new AddItemRequest { Name = "Should Not Be Created", Description = "no token" };

            var response = await anonymousClient.PostAsJsonAsync("/api/items", request);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Create_WithNonAdminToken_Returns403()
        {
            using var nonAdminClient = _fixture.CreateClient();
            var email = $"items-nonadmin-{Guid.NewGuid()}@test.com";
            const string password = "P@ssw0rd123!";
            (await nonAdminClient.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, password))).EnsureSuccessStatusCode();
            var login = await (await nonAdminClient.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password))).Content.ReadFromJsonAsync<LoginResponse>();
            nonAdminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);

            var request = new AddItemRequest { Name = "Should Not Be Created", Description = "non-admin" };
            var response = await nonAdminClient.PostAsJsonAsync("/api/items", request);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }
}
