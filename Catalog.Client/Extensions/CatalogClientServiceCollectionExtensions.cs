using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Catalog.Client.Extensions
{
    public static class CatalogClientServiceCollectionExtensions
    {
        /// <summary>
        /// Registers a typed HttpClient for reading items from Catalog.API. Every call carries the
        /// caller's own bearer token (see <see cref="BearerTokenForwardingHandler"/>) — required
        /// now that Catalog.API's item endpoints require auth — and is wrapped in the standard
        /// resilience handler (retry with jitter, circuit breaker, timeout) so a transient blip in
        /// Catalog.API doesn't fail every in-flight order-creation request.
        /// </summary>
        public static IServiceCollection AddCatalogClient(this IServiceCollection services, string baseUrl)
        {
            services.AddHttpContextAccessor();
            services.AddTransient<BearerTokenForwardingHandler>();

            services.AddHttpClient<ICatalogItemClient, CatalogItemClient>(client =>
                {
                    client.BaseAddress = new Uri(baseUrl);
                })
                .AddHttpMessageHandler<BearerTokenForwardingHandler>()
                .AddStandardResilienceHandler();

            return services;
        }
    }
}
