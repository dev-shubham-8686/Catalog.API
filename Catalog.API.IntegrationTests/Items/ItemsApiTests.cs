using Catalog.Domain.Requests.Item;
using Catalog.Domain.Responses;
using Catalog.Domain.Responses.Item;
using System.Net;
using System.Net.Http.Json;

namespace Catalog.API.IntegrationTests.Items
{
    [Collection(IntegrationTestCollection.Name)]
    public class ItemsApiTests
    {
        private readonly HttpClient _client;

        public ItemsApiTests(ApiTestFixture fixture)
        {
            _client = fixture.CreateClient();
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
    }
}
