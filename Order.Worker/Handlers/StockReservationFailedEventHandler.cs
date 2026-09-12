using Catalog.Contracts.Events;
using EventBus;
using Order.Worker.Sagas;

namespace Order.Worker.Handlers
{
    /// <summary>
    /// Thin transport adapter satisfying EventBus's IIntegrationEventHandler contract — all saga
    /// decision-making lives in IOrderSagaOrchestrator.
    /// </summary>
    public class StockReservationFailedEventHandler : IIntegrationEventHandler<StockReservationFailedIntegrationEvent>
    {
        private readonly IOrderSagaOrchestrator _orchestrator;

        public StockReservationFailedEventHandler(IOrderSagaOrchestrator orchestrator)
        {
            _orchestrator = orchestrator;
        }

        public Task HandleAsync(StockReservationFailedIntegrationEvent @event, CancellationToken cancellationToken = default) =>
            _orchestrator.HandleStockReservationFailedAsync(@event, cancellationToken);
    }
}
