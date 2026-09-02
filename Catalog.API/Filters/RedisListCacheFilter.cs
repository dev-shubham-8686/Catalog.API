using Catalog.API.Caching;
using Catalog.API.Extensions;
using Catalog.Contracts.Caching;
using Catalog.Domain.Responses;
using Catalog.Domain.Responses.Item;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Distributed;

namespace Catalog.API.Filters
{
    public class RedisListCacheFilter : IAsyncActionFilter
    {
        private readonly IDistributedCache _distributedCache;
        private readonly CacheStampedeGuard _stampedeGuard;
        private readonly DistributedCacheEntryOptions _options;

        public RedisListCacheFilter(IDistributedCache distributedCache, CacheStampedeGuard stampedeGuard, int cacheTimeSeconds)
        {
            _distributedCache = distributedCache;
            _stampedeGuard = stampedeGuard;
            _options = new DistributedCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromSeconds(cacheTimeSeconds)
            };
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (!context.ActionArguments.TryGetValue("pageSize", out var pageSizeArg) || pageSizeArg is not int pageSize ||
                !context.ActionArguments.TryGetValue("pageIndex", out var pageIndexArg) || pageIndexArg is not int pageIndex)
            {
                await next();
                return;
            }

            var key = ItemCacheKeys.GetAll(pageSize, pageIndex);

            var result = await _distributedCache.GetObjectAsync<PaginatedItemResponseModel<GetItemResponse>>(key);
            if (result != null)
            {
                context.Result = new OkObjectResult(result);
                return;
            }

            if (!await _stampedeGuard.TryEnterAsync(key))
            {
                result = await _stampedeGuard.WaitForPopulationAsync(() => _distributedCache.GetObjectAsync<PaginatedItemResponseModel<GetItemResponse>>(key));
                if (result != null)
                {
                    context.Result = new OkObjectResult(result);
                    return;
                }

                await next();
                return;
            }

            var resultContext = await next();

            if (resultContext.Result is OkObjectResult resultResponse && resultResponse.StatusCode == 200)
            {
                if (resultResponse.Value != null)
                {
                    await _distributedCache.SetObjectAsync(key, resultResponse.Value, _options);
                }
            }
        }
    }
}
