using Identity.Authentication.Contracts;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Catalog.API.IntegrationTests.Auth
{
    [Collection(IntegrationTestCollection.Name)]
    public class AuthApiTests
    {
        private readonly ApiTestFixture _fixture;
        private readonly HttpClient _client;

        public AuthApiTests(ApiTestFixture fixture)
        {
            _fixture = fixture;
            _client = fixture.CreateClient();
        }

        [Fact]
        public async Task Register_WithValidRequest_Returns201WithUser()
        {
            var request = new RegisterRequest($"user-{Guid.NewGuid()}@test.com", "P@ssw0rd123!");

            var response = await _client.PostAsJsonAsync("/api/auth/register", request);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<RegisterResponse>();
            Assert.NotNull(body);
            Assert.NotEqual(Guid.Empty, body!.UserId);
            Assert.Equal(request.Email, body.Email);
        }

        [Fact]
        public async Task Register_WithDuplicateEmail_Returns400()
        {
            var request = new RegisterRequest($"dup-{Guid.NewGuid()}@test.com", "P@ssw0rd123!");
            (await _client.PostAsJsonAsync("/api/auth/register", request)).EnsureSuccessStatusCode();

            var response = await _client.PostAsJsonAsync("/api/auth/register", request);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Login_WithValidCredentials_Returns200WithToken()
        {
            var email = $"login-{Guid.NewGuid()}@test.com";
            const string password = "P@ssw0rd123!";
            (await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, password))).EnsureSuccessStatusCode();

            var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.NotNull(body);
            Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
            Assert.True(body.ExpiresAtUtc > DateTime.UtcNow);
        }

        [Fact]
        public async Task Login_WithWrongPassword_Returns401()
        {
            var email = $"wrongpw-{Guid.NewGuid()}@test.com";
            (await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "P@ssw0rd123!"))).EnsureSuccessStatusCode();

            var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "WrongPassword!"));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Login_WithUnknownEmail_Returns401()
        {
            var response = await _client.PostAsJsonAsync(
                "/api/auth/login",
                new LoginRequest($"nobody-{Guid.NewGuid()}@test.com", "P@ssw0rd123!"));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetUsers_WithoutToken_Returns401()
        {
            var response = await _client.GetAsync("/api/auth/users");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetUsers_WithNonAdminToken_Returns403()
        {
            var email = $"nonadmin-{Guid.NewGuid()}@test.com";
            const string password = "P@ssw0rd123!";
            (await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, password))).EnsureSuccessStatusCode();
            var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
            var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
            var response = await _client.GetAsync("/api/auth/users");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task GetUsers_WithAdminToken_Returns200WithUserList()
        {
            var email = $"admin-{Guid.NewGuid()}@test.com";
            const string password = "P@ssw0rd123!";
            var token = await RegisterAdminAndLoginAsync(email, password);

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _client.GetAsync("/api/auth/users?pageSize=50&pageIndex=0");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<GetUsersResponse>();
            Assert.NotNull(body);
            Assert.Contains(body!.Users, u => u.Email == email);
        }

        /// <summary>
        /// Registers a user via the public API, then promotes it to the Admin role by talking
        /// directly to the same database the running API uses (see
        /// <see cref="ApiTestFixture.PromoteToAdminAsync"/>) — there is no self-service
        /// admin-escalation endpoint (by design), so tests exercising admin-only behavior seed
        /// the role this way.
        /// </summary>
        private async Task<string> RegisterAdminAndLoginAsync(string email, string password)
        {
            (await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, password))).EnsureSuccessStatusCode();

            await _fixture.PromoteToAdminAsync(email);

            var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
            loginResponse.EnsureSuccessStatusCode();
            var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
            return login!.AccessToken;
        }
    }
}
