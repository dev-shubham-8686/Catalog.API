using Catalog.Contracts.Events;
using EventBus;
using EventBus.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Order.Contracts.Events;

namespace Catalog.Worker.Handlers
{
    /// <summary>
    /// Reacts to a cross-service event (published by Order.Api) by attempting to reserve stock.
    /// The reservation itself is a single atomic conditional UPDATE — no distributed lock needed,
    /// and it is safe under concurrent orders for the same item. The business UPDATE and the reply
    /// event are enqueued in one DB transaction, preserving the transactional-outbox guarantee
    /// (both commit together, or neither does).
    /// </summary>
    public class OrderPlacedEventHandler : IIntegrationEventHandler<OrderPlacedIntegrationEvent>
    {
        private readonly WorkerDbContext _context;
        private readonly IOutbox _outbox;
        private readonly ILogger<OrderPlacedEventHandler> _logger;

        public OrderPlacedEventHandler(WorkerDbContext context, IOutbox outbox, ILogger<OrderPlacedEventHandler> logger)
        {
            _context = context;
            _outbox = outbox;
            _logger = logger;
        }

        public async Task HandleAsync(OrderPlacedIntegrationEvent @event, CancellationToken cancellationToken = default)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            var connection = _context.Database.GetDbConnection();

            int rowsAffected;
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction.GetDbTransaction();
                command.CommandText =
                    "UPDATE [catalog].[Items] SET AvailableStock = AvailableStock - @qty WHERE Id = @id AND AvailableStock >= @qty";

                var qtyParam = command.CreateParameter();
                qtyParam.ParameterName = "@qty";
                qtyParam.Value = @event.Quantity;
                command.Parameters.Add(qtyParam);

                var idParam = command.CreateParameter();
                idParam.ParameterName = "@id";
                idParam.Value = @event.ItemId;
                command.Parameters.Add(idParam);

                rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
            }

            if (rowsAffected == 1)
            {
                await _outbox.EnqueueAsync(new StockReservedIntegrationEvent
                {
                    OrderId = @event.OrderId,
                    ItemId = @event.ItemId,
                    Quantity = @event.Quantity
                }, cancellationToken);

                _logger.LogInformation(
                    "Reserved {Quantity} of item {ItemId} for order {OrderId}.",
                    @event.Quantity, @event.ItemId, @event.OrderId);
            }
            else
            {
                await _outbox.EnqueueAsync(new StockReservationFailedIntegrationEvent
                {
                    OrderId = @event.OrderId,
                    ItemId = @event.ItemId,
                    Quantity = @event.Quantity,
                    Reason = "Insufficient available stock."
                }, cancellationToken);

                _logger.LogInformation(
                    "Could not reserve {Quantity} of item {ItemId} for order {OrderId}: insufficient stock.",
                    @event.Quantity, @event.ItemId, @event.OrderId);
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
    }
}
