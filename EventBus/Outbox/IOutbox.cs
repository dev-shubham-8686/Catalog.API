namespace EventBus.Outbox
{
    public interface IOutbox
    {
        Task EnqueueAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : IntegrationEvent;
    }
}
