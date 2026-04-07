using Catalog.Domain.Requests.Item;
using Catalog.Domain.Responses;
using Catalog.Domain.Responses.Item;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Catalog.Domain.Services
{
    public interface IItemService
    {
        Task<GetItemsResponse> GetItemsAsync(CancellationToken cancellationToken = default);

        Task<PaginatedItemResponseModel<GetItemResponse>> GetItemsAsync(int pageSize, int pageIndex, CancellationToken cancellationToken = default);

        Task<GetItemResponse> GetItemAsync(GetItemRequest request, CancellationToken cancellationToken = default);

        Task<AddItemResponse> AddItemAsync(AddItemRequest request, CancellationToken cancellationToken = default);

        Task<EditItemResponse> EditItemAsync(Guid id, EditItemRequest request, CancellationToken cancellationToken = default);

        Task DeleteItemAsync(DeleteItemRequest request, CancellationToken cancellationToken = default);

    }
}
