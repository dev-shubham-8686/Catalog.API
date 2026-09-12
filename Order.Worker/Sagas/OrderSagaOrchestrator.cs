using Catalog.Contracts.Events;
using EventBus.Outbox;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Order.Contracts.Events;
using Order.Domain.Entities;
using Order.Domain.Repositories;

namespace Order.Worker.Sagas
{
    public class OrderSagaOrchestrator : IOrderSagaOrchestrator
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IOutbox _outbox;
        private readonly PaymentSimulationSettings _paymentSettings;
        private readonly ILogger<OrderSagaOrchestrator> _logger;

        public OrderSagaOrchestrator(
            IOrderRepository orderRepository,
            IOutbox outbox,
            IOptions<PaymentSimulationSettings> paymentSettings,
            ILogger<OrderSagaOrchestrator> logger)
        {
            _orderRepository = orderRepository;
            _outbox = outbox;
            _paymentSettings = paymentSettings.Value;
            _logger = logger;
        }

        public async Task HandleStockReservedAsync(StockReservedIntegrationEvent @event, CancellationToken cancellationToken = default)
        {
            var order = await _orderRepository.GetAsync(@event.OrderId, cancellationToken);
            if (order is null)
            {
                _logger.LogWarning("StockReserved received for unknown order {OrderId}; ignoring.", @event.OrderId);
                return;
            }

            order.Status = OrderStatus.StockReserved;

            if (SimulatePaymentSucceeds())
            {
                order.Status = OrderStatus.Confirmed;
                _logger.LogInformation("Order {OrderId}: stock reserved, payment simulated OK -> Confirmed.", order.Id);
            }
            else
            {
                // Compensating transaction: stock WAS reserved, but a later saga step failed, so we
                // must undo the reservation rather than leaving Catalog's stock count short.
                order.Status = OrderStatus.Cancelled;
                order.CancellationReason = "Simulated payment failed.";

                await _outbox.EnqueueAsync(new ReleaseStockIntegrationEvent
                {
                    OrderId = order.Id,
                    ItemId = order.ItemId,
                    Quantity = order.Quantity
                }, cancellationToken);

                _logger.LogInformation("Order {OrderId}: stock reserved, payment simulated FAILED -> Cancelled (compensating release enqueued).", order.Id);
            }

            order.UpdatedAtUtc = DateTime.UtcNow;
            await _orderRepository.SaveChangesAsync(cancellationToken);
        }

        public async Task HandleStockReservationFailedAsync(StockReservationFailedIntegrationEvent @event, CancellationToken cancellationToken = default)
        {
            var order = await _orderRepository.GetAsync(@event.OrderId, cancellationToken);
            if (order is null)
            {
                _logger.LogWarning("StockReservationFailed received for unknown order {OrderId}; ignoring.", @event.OrderId);
                return;
            }

            // Nothing to compensate — stock was never reserved.
            order.Status = OrderStatus.Cancelled;
            order.CancellationReason = $"Stock reservation failed: {@event.Reason}";
            order.UpdatedAtUtc = DateTime.UtcNow;

            await _orderRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Order {OrderId}: stock reservation failed ({Reason}) -> Cancelled.", order.Id, @event.Reason);
        }

        private bool SimulatePaymentSucceeds()
        {
            var failureRate = Math.Clamp(_paymentSettings.FailureRatePercent, 0, 100);
            return Random.Shared.Next(100) >= failureRate;
        }
    }
}
