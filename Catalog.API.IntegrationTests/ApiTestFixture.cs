using Identity.Authentication.Data;
using Identity.Authentication.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Catalog.API.IntegrationTests
{
    /// <summary>
    /// Black-box integration test fixture: hits the already-running Catalog API host (docker
    /// compose / local `dotnet run` / port-forwarded k8s Service — whatever's up) over plain
    /// HTTP, and connects directly to the same already-running SQL Server for the one thing the
    /// public API has no endpoint for (promoting a user to the Admin role).
    ///
    /// No Testcontainers, no in-process WebApplicationFactory — this tests the real, deployed
    /// stack as-is. Override the two defaults via environment variables to point at a different
    /// environment (see README.md).
    /// </summary>
    public class ApiTestFixture
    {
        private readonly Uri _baseUrl;
        private readonly string _connectionString;

        public ApiTestFixture()
        {
            _baseUrl = new Uri(Environment.GetEnvironmentVariable("API_BASE_URL") ?? "http://localhost:5000");

            _connectionString = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION_STRING")
                ?? "Server=localhost,1433;Database=Store;User Id=sa;Password=P@ssw0rd;TrustServerCertificate=True;";
        }

        /// <summary>
        /// Returns a fresh HttpClient per call — the fixture (and this method) is shared across
        /// every test in the collection, so each test gets its own client to safely set headers
        /// like Authorization without leaking state into other tests.
        /// </summary>
        public HttpClient CreateClient() => new() { BaseAddress = _baseUrl };

        /// <summary>
        /// Promotes an already-registered user to the Admin role by talking to the same
        /// database the running API uses — there is no self-service admin-escalation endpoint
        /// (by design), so tests exercising admin-only behavior seed the role this way.
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
