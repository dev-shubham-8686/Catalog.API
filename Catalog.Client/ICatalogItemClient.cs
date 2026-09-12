namespace Catalog.Client
{
    public interface ICatalogItemClient
    {
        /// <summary>
        /// Reads a single item from Catalog.API. Returns null if the item does not exist
        /// (a 404 from Catalog.API), so callers can distinguish "not found" from a transport failure
        /// (which surfaces as a thrown exception once retries/circuit-breaker are exhausted).
        /// </summary>
        Task<CatalogItemDto?> GetItemAsync(Guid itemId, CancellationToken cancellationToken = default);
    }
}
