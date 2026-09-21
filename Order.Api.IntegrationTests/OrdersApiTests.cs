using Catalog.Domain.Requests.Item;
using Catalog.Domain.Responses.Item;
using Identity.Authentication.Contracts;
using Order.Domain.Requests;
using Order.Domain.Responses;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Order.Api.IntegrationTests
{
    [Collection(IntegrationTestCollection.Name)]
    public class OrdersApiTests
    {
        private readonly OrderApiTestFixture _fixture;
        private readonly HttpClient _catalogClient;
        private readonly HttpClient _orderClient;

        public OrdersApiTests(OrderApiTestFixture fixture)
        {
            _fixture = fixture;
            _catalogClient = fixture.CreateCatalogClient();
            _orderClient = fixture.CreateOrderClient();
        }

        [Fact]
        public async Task Create_WithoutToken_Returns401()
        {
            var request = new CreateOrderRequest { ItemId = Guid.NewGuid(), Quantity = 1 };

            var response = await _orderClient.PostAsJsonAsync("/api/orders", request);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Create_WithValidToken_Returns201OwnedByCaller()
        {
            var (userId, token) = await RegisterAndLoginAsync();
            var item = await CreateCatalogItemAsync(price: 12.5m, stock: 10);

            _orderClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _orderClient.PostAsJsonAsync("/api/orders", new CreateOrderRequest { ItemId = item.Id, Quantity = 1 });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<OrderResponse>();
            Assert.NotNull(body);
            Assert.Equal(userId, body!.UserId);
            Assert.Equal(item.Id, body.ItemId);
        }

        [Fact]
        public async Task Get_AsOwner_Returns200()
        {
            var (_, token) = await RegisterAndLoginAsync();
            var item = await CreateCatalogItemAsync(price: 9.99m, stock: 5);
            _orderClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var order = await CreateOrderAsync(item.Id, 1);

            var response = await _orderClient.GetAsync($"/api/orders/{order.Id}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Get_AsDifferentUser_Returns404()
        {
            var (_, ownerToken) = await RegisterAndLoginAsync();
            var item = await CreateCatalogItemAsync(price: 9.99m, stock: 5);
            _orderClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
            var order = await CreateOrderAsync(item.Id, 1);

            var (_, otherToken) = await RegisterAndLoginAsync();
            using var otherClient = _fixture.CreateOrderClient();
            otherClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);

            var response = await otherClient.GetAsync($"/api/orders/{order.Id}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetAll_AsNonAdmin_Returns403()
        {
            var (_, token) = await RegisterAndLoginAsync();
            _orderClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _orderClient.GetAsync("/api/orders");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task GetAll_AsAdmin_Returns200WithOrders()
        {
            var (_, memberToken) = await RegisterAndLoginAsync();
            var item = await CreateCatalogItemAsync(price: 5m, stock: 5);
            _orderClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
            await CreateOrderAsync(item.Id, 1);

            var adminToken = await RegisterAdminAndLoginAsync();
            using var adminClient = _fixture.CreateOrderClient();
            adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

            var response = await adminClient.GetAsync("/api/orders");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<List<OrderResponse>>();
            Assert.NotNull(body);
            Assert.NotEmpty(body!);
        }

        [Fact]
        public async Task GetMine_ReturnsOnlyCallersOrdersEnrichedWithCatalogInfo()
        {
            var (_, myToken) = await RegisterAndLoginAsync();
            var myItem = await CreateCatalogItemAsync(price: 42.5m, stock: 3);
            _orderClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", myToken);
            var myOrder = await CreateOrderAsync(myItem.Id, 2);

            // A different user's order must not leak into my "mine" list.
            var (_, otherToken) = await RegisterAndLoginAsync();
            var otherItem = await CreateCatalogItemAsync(price: 7m, stock: 3);
            using var otherClient = _fixture.CreateOrderClient();
            otherClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);
            await otherClient.PostAsJsonAsync("/api/orders", new CreateOrderRequest { ItemId = otherItem.Id, Quantity = 1 });

            var response = await _orderClient.GetAsync("/api/orders/me");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<List<OrderWithItemResponse>>();
            Assert.NotNull(body);
            Assert.All(body!, o => Assert.NotEqual(otherItem.Id, o.ItemId));
            var mine = Assert.Single(body!, o => o.Id == myOrder.Id);
            Assert.Equal(myItem.Name, mine.ItemName);
            Assert.Equal(myItem.Price, mine.CurrentItemPrice);
        }

        private async Task<OrderResponse> CreateOrderAsync(Guid itemId, int quantity)
        {
            var response = await _orderClient.PostAsJsonAsync("/api/orders", new CreateOrderRequest { ItemId = itemId, Quantity = quantity });
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<OrderResponse>())!;
        }

        private async Task<(Guid UserId, string Token)> RegisterAndLoginAsync()
        {
            var email = $"order-user-{Guid.NewGuid()}@test.com";
            const string password = "P@ssw0rd123!";

            var registerResponse = await _catalogClient.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, password));
            registerResponse.EnsureSuccessStatusCode();
            var registered = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>();

            var loginResponse = await _catalogClient.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
            loginResponse.EnsureSuccessStatusCode();
            var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

            return (registered!.UserId, login!.AccessToken);
        }

        private async Task<string> RegisterAdminAndLoginAsync()
        {
            var email = $"order-admin-{Guid.NewGuid()}@test.com";
            const string password = "P@ssw0rd123!";

            (await _catalogClient.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, password))).EnsureSuccessStatusCode();
            await _fixture.PromoteToAdminAsync(email);

            var loginResponse = await _catalogClient.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
            loginResponse.EnsureSuccessStatusCode();
            var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
            return login!.AccessToken;
        }

        /// <summary>
        /// Creates a real, orderable catalog item (with price/stock) via Catalog.API, using a
        /// fresh Admin token — Catalog.API's item-create endpoint is Admin-only.
        /// </summary>
        private async Task<GetItemResponse> CreateCatalogItemAsync(decimal price, int stock)
        {
            var adminToken = await RegisterAdminAndLoginAsync();
            using var adminCatalogClient = _fixture.CreateCatalogClient();
            adminCatalogClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

            var request = new AddItemRequest
            {
                Name = $"Order Test Item {Guid.NewGuid()}",
                Description = "Order.Api.IntegrationTests",
                Price = price,
                AvailableStock = stock
            };

            var response = await adminCatalogClient.PostAsJsonAsync("/api/items", request);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<GetItemResponse>())!;
        }
    }
}
