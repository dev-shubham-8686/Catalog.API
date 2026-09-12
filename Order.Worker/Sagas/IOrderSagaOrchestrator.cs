using Catalog.Contracts.Events;

namespace Order.Worker.Sagas
{
    /// <summary>
    /// Owns every state transition of the place-order saga after the initial OrderPlaced step.
    /// Unlike Catalog.Worker's Item* handlers (choreography — each handler reacts independently
    /// with no shared coordinator), this orchestrator is the single place that decides what
    /// happens next for a given order, based on which reply event Catalog.Worker sent back.
    /// </summary>
    public interface IOrderSagaOrchestrator
    {
        Task HandleStockReservedAsync(StockReservedIntegrationEvent @event, CancellationToken cancellationToken = default);
        Task HandleStockReservationFailedAsync(StockReservationFailedIntegrationEvent @event, CancellationToken cancellationToken = default);
    }
}
