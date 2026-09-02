namespace Catalog.Contracts.Caching
{
    public static class ItemCacheKeys
    {
        public static string Get(Guid itemId) => $"Item.Get.{itemId}";

        public static string GetAll(int pageSize, int pageIndex) => $"Item.GetAll.{pageSize}.{pageIndex}";
    }
}
