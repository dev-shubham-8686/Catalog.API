using EventBus;
using Microsoft.EntityFrameworkCore;
using Order.Contracts.Events;

namespace Catalog.Worker.Handlers
{
    /// <summary>
    /// Compensating handler: releases a stock reservation that Order.Worker's saga orchestrator
    /// decided to undo after a later step (simulated payment) failed. A plain unconditional
    /// increment is safe here — the reservation is only ever released once per order, since the
    /// orchestrator only enqueues this event from the single terminal "payment failed" transition.
    /// </summary>
    public class ReleaseStockEventHandler : IIntegrationEventHandler<ReleaseStockIntegrationEvent>
    {
        private readonly WorkerDbContext _context;
        private readonly ILogger<ReleaseStockEventHandler> _logger;

        public ReleaseStockEventHandler(WorkerDbContext context, ILogger<ReleaseStockEventHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task HandleAsync(ReleaseStockIntegrationEvent @event, CancellationToken cancellationToken = default)
        {
            var connection = _context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var command = connection.CreateCommand();
            command.CommandText = "UPDATE [catalog].[Items] SET AvailableStock = AvailableStock + @qty WHERE Id = @id";

            var qtyParam = command.CreateParameter();
            qtyParam.ParameterName = "@qty";
            qtyParam.Value = @event.Quantity;
            command.Parameters.Add(qtyParam);

            var idParam = command.CreateParameter();
            idParam.ParameterName = "@id";
            idParam.Value = @event.ItemId;
            command.Parameters.Add(idParam);

            await command.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogInformation(
                "Released {Quantity} of item {ItemId} back to stock for cancelled order {OrderId}.",
                @event.Quantity, @event.ItemId, @event.OrderId);
        }
    }
}
