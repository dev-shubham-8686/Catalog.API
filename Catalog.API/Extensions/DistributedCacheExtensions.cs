using Catalog.Domain.Configurations;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace Catalog.API.Extensions
{
    public static class DistributedCacheExtensions
    {
        private static readonly JsonSerializerSettings _serializerSettings = CreateSettings();

        public static IServiceCollection AddDistributedRedisCache(this IServiceCollection services,
            IConfiguration configuration)
        {

            var settings = configuration.GetSection("CacheSettings");

            var settingsTyped = settings.Get<CacheSettings>();

            services.Configure<CacheSettings>(settings); //Register CacheSettings with Options Pattern

           //public MyService(IOptions<CacheSettings> options)
           //{
           //    var conn = options.Value.ConnectionString;
           //}

            var connectionString = settingsTyped?.ConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("CacheSettings:ConnectionString is required to enable Redis distributed cache.");
            }

            services.AddDistributedRedisCache(options => { options.Configuration = connectionString; });

            return services;
        }

        public static async Task<T?> GetObjectAsync<T>(this IDistributedCache cache, string key)
        {
            var json = await cache.GetStringAsync(key);

            return json == null ? default : JsonConvert.DeserializeObject<T>(json, _serializerSettings);
        }

        public static async Task SetObjectAsync(this IDistributedCache cache, string key, object value)
        {
            var json = JsonConvert.SerializeObject(value, _serializerSettings);
            await cache.SetStringAsync(key, json);
        }

        public static async Task SetObjectAsync(
            this IDistributedCache cache,
            string key,
            object value,
            DistributedCacheEntryOptions options)
        {
            var json = JsonConvert.SerializeObject(value, _serializerSettings);
            await cache.SetStringAsync(key, json, options);
        }

        private static JsonSerializerSettings CreateSettings()
        {
            return new JsonSerializerSettings();
        }
    }
}
