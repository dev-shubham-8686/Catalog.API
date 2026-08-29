namespace EventBus.Idempotency
{
    public interface IIdempotencyStore
    {
        /// <summary>
        /// Attempts to mark an event as processed. Returns false if it was already processed
        /// (by this or another consumer instance), meaning the caller should skip handling it.
        /// </summary>
        Task<bool> TryMarkProcessedAsync(Guid eventId, string eventType, CancellationToken cancellationToken = default);
    }
}
