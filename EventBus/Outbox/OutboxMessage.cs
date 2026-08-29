namespace EventBus.Outbox
{
    public class OutboxMessage
    {
        public Guid Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime OccurredOnUtc { get; set; }
        public DateTime? ProcessedOnUtc { get; set; }
        public string? Error { get; set; }
        public int RetryCount { get; set; }
        public DateTime? LockedUntilUtc { get; set; }
        public string? LockedBy { get; set; }
    }
}
