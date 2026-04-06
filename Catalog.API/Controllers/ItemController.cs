using Catalog.Domian.Requests.Item;
using Catalog.Domian.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ItemController : ControllerBase
    {
        private readonly IItemService _itemService;

        public ItemController(IItemService itemService)
        {
            _itemService = itemService;
        }

        [HttpGet]
        public async Task<IActionResult> GetItemsAsync(CancellationToken cancellationToken = default)
        {
            var response = await _itemService.GetItemsAsync(cancellationToken);
            return Ok(response);
        }

        [HttpGet("{id}")]
         public async Task<IActionResult> GetItemAsync([FromRoute] Guid id, CancellationToken cancellationToken = default)
        {
            var request = new GetItemRequest { Id = id };   

            var response = await _itemService.GetItemAsync(request, cancellationToken);
            return Ok(response);
        }

        [HttpPost]
         public async Task<IActionResult> AddItemAsync([FromBody] AddItemRequest request, CancellationToken cancellationToken = default)
        {
            var response = await _itemService.AddItemAsync(request, cancellationToken);
            return Ok(response);
        }

        [HttpPut]
         public async Task<IActionResult> EditItemAsync([FromBody] EditItemRequest request, CancellationToken cancellationToken = default)
        {
            var response = await _itemService.EditItemAsync(request, cancellationToken);
            return Ok(response);
        }
        [HttpDelete("{id}")]
         public async Task<IActionResult> DeleteItemAsync([FromRoute] Guid id, CancellationToken cancellationToken = default)
        {
            var request = new DeleteItemRequest { Id = id };

            await _itemService.DeleteItemAsync(request, cancellationToken);
            return NoContent();
        }
    }
}
