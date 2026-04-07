using Catalog.Domain.Entities;
using Catalog.Domain.Requests.Item;
using Catalog.Domain.Responses;
using Catalog.Domain.Responses.Item;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Catalog.Domain.Mappers
{
    public static class ContractMapping
    {
        #region Item

        public static AddItemResponse MapToAddItemResponse(this Item item)
        {
            return new AddItemResponse
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description
            };
        }

        public static EditItemResponse MapToEditItemResponse(this Item item, Guid id)
        {
            return new EditItemResponse
            {
                Id = id,
                Name = item.Name,
                Description = item.Description
            };
        }

        public static GetItemResponse MapToGetItemResponse(this Item item)
        {
            return new GetItemResponse
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description
            };
        }

        public static GetItemsResponse MapToGetItemsResponse(this IEnumerable<Item> items)
        {
            return new GetItemsResponse
            {
                Items = items.Select(item => item.MapToGetItemResponse()).ToList()
            };
        }

        public static Item MapToItem(this AddItemRequest request)
        {
            return new Item
            {
                Name = request.Name,
                Description = request.Description
            };
        }

        public static Item MapToItem(this EditItemRequest request, Guid id)
        {
            return new Item
            {
                Id = id,
                Name = request.Name,
                Description = request.Description
            };
        }

        public static PaginatedItemResponseModel<GetItemResponse> MapToPaginatedItemResponseModel(this IEnumerable<Item> items, int pageSize, int pageIndex, long totalItems)
        {
            return new PaginatedItemResponseModel<GetItemResponse>(
                pageIndex, pageSize, totalItems, items.MapToGetItemsResponse().Items);
        }

        #endregion
    }
}
