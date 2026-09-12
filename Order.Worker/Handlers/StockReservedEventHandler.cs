using Catalog.Contracts.Events;
using EventBus;
using Order.Worker.Sagas;

namespace Order.Worker.Handlers
{
    /// <summary>
    /// Thin transport adapter satisfying EventBus's IIntegrationEventHandler contract — all saga
    /// decision-making lives in IOrderSagaOrchestrator.
    /// </summary>
    public class StockReservedEventHandler : IIntegrationEventHandler<StockReservedIntegrationEvent>
    {
        private readonly IOrderSagaOrchestrator _orchestrator;

        public StockReservedEventHandler(IOrderSagaOrchestrator orchestrator)
        {
            _orchestrator = orchestrator;
        }

        public Task HandleAsync(StockReservedIntegrationEvent @event, CancellationToken cancellationToken = default) =>
            _orchestrator.HandleStockReservedAsync(@event, cancellationToken);
    }
}
