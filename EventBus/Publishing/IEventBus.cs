using EventBus.Outbox;

namespace EventBus.Publishing
{
    public interface IEventBus
    {
        Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default);
    }
}
