using Microsoft.AspNetCore.Mvc;
using Order.Domain.Requests;
using Order.Domain.Responses;
using Order.Domain.Services;

namespace Order.Api.Controllers
{
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;

        public OrdersController(IOrderService orderService)
        {
            _orderService = orderService;
        }

        [HttpPost(ApiEndpoints.Orders.Create)]
        [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] CreateOrderRequest request, CancellationToken cancellationToken = default)
        {
            var result = await _orderService.CreateOrderAsync(request, cancellationToken);

            if (!result.Succeeded)
            {
                return BadRequest(new { error = result.Error });
            }

            return CreatedAtAction(nameof(Get), new { id = result.Order!.Id }, result.Order);
        }

        [HttpGet(ApiEndpoints.Orders.Get)]
        [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Get([FromRoute] Guid id, CancellationToken cancellationToken = default)
        {
            var order = await _orderService.GetOrderAsync(id, cancellationToken);

            if (order is null)
            {
                return NotFound();
            }

            return Ok(order);
        }

        [HttpGet(ApiEndpoints.Orders.GetAll)]
        [ProducesResponseType(typeof(IEnumerable<OrderResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default)
        {
            var orders = await _orderService.GetOrdersAsync(cancellationToken);
            return Ok(orders);
        }
    }
}
