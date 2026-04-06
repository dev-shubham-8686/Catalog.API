using Catalog.Domian.Requests.Item;
using Catalog.Domian.Responses.Item;
using Catalog.Domian.Services;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.API.Controllers
{
    //[Route("api/[controller]")]
    [ApiController]
    public class ItemController : ControllerBase
    {
        private readonly IItemService _itemService;

        public ItemController(IItemService itemService)
        {
            _itemService = itemService;
        }

        [HttpGet(ApiEndpoints.Movies.GetAll)]
        [ProducesResponseType(typeof(GetItemsResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default)
        {
            var response = await _itemService.GetItemsAsync(cancellationToken);
            return Ok(response);
        }

        [HttpGet(ApiEndpoints.Movies.Get)]
        [ProducesResponseType(typeof(GetItemResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Get([FromRoute] Guid id, CancellationToken cancellationToken = default)
        {

            var response = await _itemService.GetItemAsync(new GetItemRequest { Id = id }, cancellationToken);

            if (response is null)
            {
                return NotFound();
            }

            return Ok(response);
        }

        [HttpPost(ApiEndpoints.Movies.Create)]
        [ProducesResponseType(typeof(AddItemResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Create([FromBody] AddItemRequest request, CancellationToken cancellationToken = default)
        {
            var response = await _itemService.AddItemAsync(request, cancellationToken);

            //return Ok(response);

            var getItemResponse = await _itemService.GetItemAsync(new GetItemRequest { Id = response.Id }, cancellationToken);

            return CreatedAtAction(nameof(Get), new { id = response.Id }, getItemResponse);
        }

        [HttpPut(ApiEndpoints.Movies.Update)]
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

        [HttpDelete(ApiEndpoints.Movies.Delete)]
        [ProducesResponseType(typeof(void), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken cancellationToken = default)
        {
          
            await _itemService.DeleteItemAsync(new DeleteItemRequest { Id = id }, cancellationToken);

            //if (!deleted)
            //{
            //    return NotFound();
            //}

            return Ok();
        }
    }
}
