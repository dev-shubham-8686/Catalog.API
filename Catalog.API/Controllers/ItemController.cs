using Catalog.API.Extensions;
using Catalog.API.Filters;
using Catalog.Domain.Requests.Item;
using Catalog.Domain.Responses.Item;
using Catalog.Domain.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;

namespace Catalog.API.Controllers
{
    
    [ApiController]
    //[JsonException]
    public class ItemController : ControllerBase
    {
        private readonly IItemService _itemService;
        private readonly IDistributedCache _distributedCache;
        private readonly ILogger<ItemController> _logger;

        public ItemController(IItemService itemService, IDistributedCache distributedCache, ILogger<ItemController> logger)
        {
            _itemService = itemService;
            _distributedCache = distributedCache;
            _logger = logger;
        }

        [HttpGet(ApiEndpoints.Items.GetAll)]
        [ProducesResponseType(typeof(GetItemsResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default)
        {
            var response = await _itemService.GetItemsAsync(cancellationToken);
            return Ok(response);
        }

        #region InController Redis Cache


        //[HttpGet(ApiEndpoints.Items.Get)]
        //[ProducesResponseType(typeof(GetItemResponse), StatusCodes.Status200OK)]
        //[ProducesResponseType(StatusCodes.Status404NotFound)]
        //[ResponseCache(Duration = 100, VaryByQueryKeys = new[] { "*" })]
        ////[TypeFilter(typeof(RedisCacheFilter), Arguments = new object[] { 20 })]
        //public async Task<IActionResult> Get([FromRoute] Guid id, CancellationToken cancellationToken = default)
        //{
        //    var key = $"{typeof(ItemController).FullName}.{nameof(Get)}.{id}";

        //    var cachedResult = await _distributedCache.
        //        GetObjectAsync<GetItemResponse>(key);

        //    if (cachedResult != null)
        //    {
        //        return Ok(cachedResult);
        //    }

        //    var response = await _itemService.GetItemAsync(new GetItemRequest { Id = id }, cancellationToken);

        //    if (response is null)
        //    {
        //        return NotFound();
        //    }

        //    await _distributedCache.SetObjectAsync(key, response);

        //    return Ok(response);
        //}

        #endregion

        [HttpGet(ApiEndpoints.Items.Get)]
        [ProducesResponseType(typeof(GetItemResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ResponseCache(Duration = 100, VaryByQueryKeys = new[] { "*" })]
        [TypeFilter(typeof(RedisCacheFilter), Arguments = new object[] { 20 })]
        public async Task<IActionResult> Get([FromRoute] Guid id, CancellationToken cancellationToken = default)
        {

            var response = await _itemService.GetItemAsync(new GetItemRequest { Id = id }, cancellationToken);

            return Ok(response);
        }

        [HttpPost(ApiEndpoints.Items.Create)]
        [ProducesResponseType(typeof(AddItemResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Create([FromBody] AddItemRequest request, CancellationToken cancellationToken = default)
        {
            var response = await _itemService.AddItemAsync(request, cancellationToken);

            var getItemResponse = await _itemService.GetItemAsync(new GetItemRequest { Id = response.Id }, cancellationToken);

            return CreatedAtAction(nameof(Get), new { id = response.Id }, getItemResponse);
        }

        [HttpPut(ApiEndpoints.Items.Update)]
        [ProducesResponseType(typeof(EditItemResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] EditItemRequest request, CancellationToken cancellationToken = default)
        {
            var response = await _itemService.EditItemAsync(id, request, cancellationToken);

            return Ok(response);
        }

        [HttpDelete(ApiEndpoints.Items.Delete)]
        [ProducesResponseType(typeof(void), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken cancellationToken = default)
        {
     
            await _itemService.DeleteItemAsync(new DeleteItemRequest { Id = id }, cancellationToken);

            return Ok();
        }
    }
}
