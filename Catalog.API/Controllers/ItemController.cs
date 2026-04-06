using Catalog.API.Filters;
using Catalog.Domain.Requests.Item;
using Catalog.Domain.Responses.Item;
using Catalog.Domain.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;

namespace Catalog.API.Controllers
{
    
    [ApiController]
    public class ItemController : ControllerBase
    {
        private readonly IItemService _itemService;
        private readonly IDistributedCache _distributedCache;

        public ItemController(IItemService itemService, IDistributedCache distributedCache)
        {
            _itemService = itemService;
            _distributedCache = distributedCache;
        }

        [HttpGet(ApiEndpoints.Items.GetAll)]
        [ProducesResponseType(typeof(GetItemsResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default)
        {
            var response = await _itemService.GetItemsAsync(cancellationToken);
            return Ok(response);
        }

        [HttpGet(ApiEndpoints.Items.Get)]
        [ProducesResponseType(typeof(GetItemResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ResponseCache(Duration = 100, VaryByQueryKeys = new[] { "*" })]
        [TypeFilter(typeof(RedisCacheFilter), Arguments = new object[] { 20 })]
        public async Task<IActionResult> Get([FromRoute] Guid id, CancellationToken cancellationToken = default)
        {

            var response = await _itemService.GetItemAsync(new GetItemRequest { Id = id }, cancellationToken);

            if (response is null)
            {
                return NotFound();
            }

            return Ok(response);
        }

        [HttpPost(ApiEndpoints.Items.Create)]
        [ProducesResponseType(typeof(AddItemResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Create([FromBody] AddItemRequest request, CancellationToken cancellationToken = default)
        {
            var response = await _itemService.AddItemAsync(request, cancellationToken);

            //return Ok(response);

            var getItemResponse = await _itemService.GetItemAsync(new GetItemRequest { Id = response.Id }, cancellationToken);

            return CreatedAtAction(nameof(Get), new { id = response.Id }, getItemResponse);
        }

        [HttpPut(ApiEndpoints.Items.Update)]
        [ProducesResponseType(typeof(EditItemResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update([FromBody] EditItemRequest request, CancellationToken cancellationToken = default)
        {
            var response = await _itemService.EditItemAsync(request, cancellationToken);

            if(response is null)
            {
                return NotFound();
            }

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
