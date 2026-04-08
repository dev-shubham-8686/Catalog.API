using Catalog.API.Extensions;
using Catalog.Domain.Responses.Item;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Distributed;

namespace Catalog.API.Filters
{
    public class RedisCacheFilter: IAsyncActionFilter
    {
        private readonly IDistributedCache _distributedCache;
        private readonly DistributedCacheEntryOptions _options;

        public RedisCacheFilter(IDistributedCache distributedCache, int cacheTimeSeconds)
        {
            _distributedCache = distributedCache;
            _options = new DistributedCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromSeconds(cacheTimeSeconds)
            };
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            if (!context.ActionArguments.ContainsKey("id"))
            {
                await next();
                return;
            }

            var actionName = context.RouteData.Values["action"] as string;
            var controllerName = context.RouteData.Values["controller"] as string;
            var id = context.ActionArguments["id"];

            if (string.IsNullOrWhiteSpace(actionName) || string.IsNullOrWhiteSpace(controllerName))
            {
                await next();
                return;
            }

            var key = $"{controllerName}.{actionName}.{id}";


            var result = await _distributedCache.GetObjectAsync<GetItemResponse>(key);

            if (result != null)
            {
                context.Result = new OkObjectResult(result);
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
