namespace EventBus.Consuming
{
    public record EventSubscription(Type EventType, Type HandlerType);

    public class EventBusSubscriptionManager
    {
        private readonly Dictionary<string, EventSubscription> _subscriptions = new();

        public EventBusSubscriptionManager Subscribe<TEvent, THandler>()
            where TEvent : IntegrationEvent
            where THandler : IIntegrationEventHandler<TEvent>
        {
            _subscriptions[typeof(TEvent).Name] = new EventSubscription(typeof(TEvent), typeof(THandler));
            return this;
        }

        public IReadOnlyCollection<EventSubscription> Subscriptions => _subscriptions.Values.ToList();

        public bool TryGetSubscription(string eventTypeName, out EventSubscription subscription)
        {
            return _subscriptions.TryGetValue(eventTypeName, out subscription!);
        }
    }
}
