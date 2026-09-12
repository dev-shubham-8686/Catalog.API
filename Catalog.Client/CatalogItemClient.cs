using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Catalog.Client
{
    public class CatalogItemClient : ICatalogItemClient
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly HttpClient _httpClient;

        public CatalogItemClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<CatalogItemDto?> GetItemAsync(Guid itemId, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.GetAsync($"api/items/{itemId}", cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<CatalogItemDto>(SerializerOptions, cancellationToken);
        }
    }
}
