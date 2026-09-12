using Microsoft.Extensions.DependencyInjection;

namespace Catalog.Client.Extensions
{
    public static class CatalogClientServiceCollectionExtensions
    {
        /// <summary>
        /// Registers a typed HttpClient for reading items from Catalog.API, wrapped in the standard
        /// resilience handler (retry with jitter, circuit breaker, timeout) so a transient blip in
        /// Catalog.API doesn't fail every in-flight order-creation request.
        /// </summary>
        public static IServiceCollection AddCatalogClient(this IServiceCollection services, string baseUrl)
        {
            services.AddHttpClient<ICatalogItemClient, CatalogItemClient>(client =>
                {
                    client.BaseAddress = new Uri(baseUrl);
                })
                .AddStandardResilienceHandler();

            return services;
        }
    }
}
