namespace Catalog.Client
{
    public class CatalogItemDto
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public decimal? Price { get; set; }
        public int? AvailableStock { get; set; }
    }
}
