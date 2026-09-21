using Identity.Authentication.Data;
using Identity.Authentication.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Order.Api.IntegrationTests
{
    /// <summary>
    /// Black-box fixture for Order.Api, mirroring Catalog.API.IntegrationTests' ApiTestFixture.
    /// Two base URLs are needed because the identity provider is Catalog.API (its mounted
    /// Identity.Authentication AuthController), while the system under test is Order.Api — a
    /// token minted against Catalog.API is valid on Order.Api too, since both trust the same
    /// Jwt:Key/Issuer/Audience (decentralized validation, see the auth plan).
    /// </summary>
    public class OrderApiTestFixture
    {
        private readonly string _connectionString;

        public Uri CatalogBaseUrl { get; }
        public Uri OrderBaseUrl { get; }

        public OrderApiTestFixture()
        {
            CatalogBaseUrl = new Uri(Environment.GetEnvironmentVariable("CATALOG_API_BASE_URL") ?? "http://localhost:5000");
            OrderBaseUrl = new Uri(Environment.GetEnvironmentVariable("ORDER_API_BASE_URL") ?? "http://localhost:5300");

            _connectionString = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION_STRING")
                ?? "Server=localhost,1433;Database=Store;User Id=sa;Password=P@ssw0rd;TrustServerCertificate=True;";
        }

        public HttpClient CreateCatalogClient() => new() { BaseAddress = CatalogBaseUrl };

        public HttpClient CreateOrderClient() => new() { BaseAddress = OrderBaseUrl };

        /// <summary>
        /// Promotes an already-registered user to the Admin role by talking directly to the same
        /// Identity database Catalog.API uses (there is no self-service admin-escalation
        /// endpoint) — identical approach to Catalog.API.IntegrationTests' ApiTestFixture.
        /// </summary>
        public async Task PromoteToAdminAsync(string email)
        {
            var services = new ServiceCollection();
            services.AddDbContext<IdentityDataContext>(opt => opt.UseSqlServer(_connectionString));
            services.AddLogging();
            services
                .AddIdentityCore<ApplicationUser>()
                .AddRoles<IdentityRole<Guid>>()
                .AddEntityFrameworkStores<IdentityDataContext>();

            await using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();

            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            if (!await roleManager.RoleExistsAsync(Identity.Authentication.Roles.Admin))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(Identity.Authentication.Roles.Admin));
            }

            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            await userManager.AddToRoleAsync(user!, Identity.Authentication.Roles.Admin);
        }
    }
}
