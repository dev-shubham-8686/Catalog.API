namespace Catalog.API.Caching
{
    /// <summary>
    /// Shared cache-stampede protection for action filters: only one caller per key fetches from
    /// the DB and populates the cache; everyone else polls briefly for the result instead of all
    /// hitting the DB at once. Bounded so a crashed/slow populator never blocks callers forever.
    /// </summary>
    public class CacheStampedeGuard
    {
        private static readonly TimeSpan LockTtl = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);
        private static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(2);

        private readonly IRedisLockService _lockService;

        public CacheStampedeGuard(IRedisLockService lockService)
        {
            _lockService = lockService;
        }

        /// <summary>True if the caller now owns the lock and should fetch + populate the cache.</summary>
        public Task<bool> TryEnterAsync(string key) => _lockService.TryAcquireAsync(key, LockTtl);

        /// <summary>
        /// Polls the cache for up to <see cref="MaxWait"/> for the lock holder to populate it.
        /// Returns the value once found, or null if the wait budget is exhausted (caller should
        /// then fetch directly rather than wait further).
        /// </summary>
        public async Task<T?> WaitForPopulationAsync<T>(Func<Task<T?>> cacheRead) where T : class
        {
            var elapsed = TimeSpan.Zero;
            while (elapsed < MaxWait)
            {
                await Task.Delay(PollInterval);
                elapsed += PollInterval;

                var cached = await cacheRead();
                if (cached != null)
                {
                    return cached;
                }
            }

            return null;
        }
    }
}
