using StackExchange.Redis;

namespace Catalog.API.Caching
{
    public interface IRedisLockService
    {
        /// <summary>
        /// Attempts to acquire a short-lived, auto-expiring lock for <paramref name="key"/>.
        /// Returns true if the caller now owns the lock (and should populate the cache), false if
        /// someone else already holds it.
        /// </summary>
        Task<bool> TryAcquireAsync(string key, TimeSpan ttl);
    }

    public class RedisLockService : IRedisLockService
    {
        private readonly IConnectionMultiplexer _connectionMultiplexer;

        public RedisLockService(IConnectionMultiplexer connectionMultiplexer)
        {
            _connectionMultiplexer = connectionMultiplexer;
        }

        public Task<bool> TryAcquireAsync(string key, TimeSpan ttl)
        {
            var db = _connectionMultiplexer.GetDatabase();
            return db.StringSetAsync($"lock:{key}", Environment.MachineName, ttl, When.NotExists);
        }
    }
}
