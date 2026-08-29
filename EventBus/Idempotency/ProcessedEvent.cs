namespace EventBus.Idempotency
{
    public class ProcessedEvent
    {
        public Guid EventId { get; set; }
        public string EventType { get; set; } = string.Empty;
        public DateTime ProcessedOnUtc { get; set; }
    }
}
