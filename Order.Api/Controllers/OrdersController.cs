using Identity.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Order.Domain.Requests;
using Order.Domain.Responses;
using Order.Domain.Services;

namespace Order.Api.Controllers
{
    [ApiController]
    [Authorize]
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
            // The caller can never place an order "as" someone else — the owning user always
            // comes from their own token, never from the request body.
            var userId = User.GetUserId()!.Value;

            var result = await _orderService.CreateOrderAsync(request, userId, cancellationToken);

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

            // Owner-or-admin only. A 404 (not 403) for a mismatched owner avoids confirming that
            // another user's order exists at all.
            var callerId = User.GetUserId();
            if (order.UserId != callerId && !User.IsInRole(Roles.Admin))
            {
                return NotFound();
            }

            return Ok(order);
        }

        [HttpGet(ApiEndpoints.Orders.GetAll)]
        [Authorize(Policy = AuthPolicyNames.AdminOnly)]
        [ProducesResponseType(typeof(IEnumerable<OrderResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default)
        {
            var orders = await _orderService.GetOrdersAsync(cancellationToken);
            return Ok(orders);
        }

        [HttpGet(ApiEndpoints.Orders.Mine)]
        [ProducesResponseType(typeof(IEnumerable<OrderWithItemResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMine(CancellationToken cancellationToken = default)
        {
            var userId = User.GetUserId()!.Value;

            var orders = await _orderService.GetOrdersForUserAsync(userId, cancellationToken);
            return Ok(orders);
        }
    }
}
