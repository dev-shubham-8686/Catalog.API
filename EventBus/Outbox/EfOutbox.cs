using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EventBus.Outbox
{
    public class EfOutbox<TContext> : IOutbox where TContext : DbContext
    {
        private readonly TContext _context;

        public EfOutbox(TContext context)
        {
            _context = context;
        }

        public async Task EnqueueAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : IntegrationEvent
        {
            var message = new OutboxMessage
            {
                Id = @event.Id,
                Type = typeof(TEvent).Name,
                Content = JsonSerializer.Serialize(@event, @event.GetType()),
                OccurredOnUtc = @event.OccurredOnUtc
            };

            await _context.Set<OutboxMessage>().AddAsync(message, cancellationToken);
        }
    }
}
