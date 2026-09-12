using Catalog.Client;
using EventBus.Outbox;
using Microsoft.Extensions.Logging;
using Order.Contracts.Events;
using Order.Domain.Mappers;
using Order.Domain.Repositories;
using Order.Domain.Requests;
using Order.Domain.Responses;

namespace Order.Domain.Services
{
    public class OrderService : IOrderService
    {
        private readonly IOrderRepository _orderRepository;
        private readonly ICatalogItemClient _catalogItemClient;
        private readonly IOutbox _outbox;
        private readonly ILogger<OrderService> _logger;

        public OrderService(
            IOrderRepository orderRepository,
            ICatalogItemClient catalogItemClient,
            IOutbox outbox,
            ILogger<OrderService> logger)
        {
            _orderRepository = orderRepository;
            _catalogItemClient = catalogItemClient;
            _outbox = outbox;
            _logger = logger;
        }

        public async Task<CreateOrderResult> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (request.Quantity <= 0)
            {
                return CreateOrderResult.Failure("Quantity must be greater than zero.");
            }

            // Synchronous cross-service read: Order.Api calls Catalog.API directly (via the typed
            // Catalog.Client, with retry/circuit-breaker built in) to snapshot the current price and
            // do a fast-fail stock check before even creating the order. This is *not* the
            // authoritative stock check — that happens as an atomic conditional UPDATE in
            // Catalog.Worker once OrderPlacedIntegrationEvent is consumed, which is safe under
            // concurrent orders. This pre-check only exists to reject obviously-bad requests
            // immediately instead of always round-tripping through the async saga first.
            var item = await _catalogItemClient.GetItemAsync(request.ItemId, cancellationToken);
            if (item is null)
            {
                return CreateOrderResult.Failure($"Item '{request.ItemId}' was not found in the catalog.");
            }

            if (item.Price is null)
            {
                return CreateOrderResult.Failure($"Item '{request.ItemId}' has no price set and cannot be ordered.");
            }

            if (item.AvailableStock is null || item.AvailableStock < request.Quantity)
            {
                return CreateOrderResult.Failure($"Insufficient stock for item '{request.ItemId}'.");
            }

            var order = new Entities.Order
            {
                Id = Guid.NewGuid(),
                ItemId = request.ItemId,
                Quantity = request.Quantity,
                UnitPriceSnapshot = item.Price.Value,
                Status = Entities.OrderStatus.Pending
            };

            await _orderRepository.AddAsync(order, cancellationToken);

            await _outbox.EnqueueAsync(new OrderPlacedIntegrationEvent
            {
                OrderId = order.Id,
                ItemId = order.ItemId,
                Quantity = order.Quantity
            }, cancellationToken);

            await _orderRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Order {OrderId} placed for item {ItemId} (qty {Quantity}).", order.Id, order.ItemId, order.Quantity);

            return CreateOrderResult.Success(order.MapToOrderResponse());
        }

        public async Task<OrderResponse?> GetOrderAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var order = await _orderRepository.GetAsync(id, cancellationToken);
            return order?.MapToOrderResponse();
        }

        public async Task<IEnumerable<OrderResponse>> GetOrdersAsync(CancellationToken cancellationToken = default)
        {
            var orders = await _orderRepository.GetAsync(cancellationToken);
            return orders.Select(o => o.MapToOrderResponse());
        }
    }
}
