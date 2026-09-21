using Catalog.Client;
using Order.Domain.Responses;
using OrderEntity = Order.Domain.Entities.Order;

namespace Order.Domain.Mappers
{
    public static class OrderMapping
    {
        public static OrderResponse MapToOrderResponse(this OrderEntity order)
        {
            return new OrderResponse
            {
                Id = order.Id,
                UserId = order.UserId,
                ItemId = order.ItemId,
                Quantity = order.Quantity,
                UnitPriceSnapshot = order.UnitPriceSnapshot,
                Status = order.Status,
                CancellationReason = order.CancellationReason,
                CreatedAtUtc = order.CreatedAtUtc,
                UpdatedAtUtc = order.UpdatedAtUtc
            };
        }

        public static OrderWithItemResponse MapToOrderWithItemResponse(this OrderEntity order, CatalogItemDto? item)
        {
            return new OrderWithItemResponse
            {
                Id = order.Id,
                UserId = order.UserId,
                ItemId = order.ItemId,
                Quantity = order.Quantity,
                UnitPriceSnapshot = order.UnitPriceSnapshot,
                Status = order.Status,
                CancellationReason = order.CancellationReason,
                CreatedAtUtc = order.CreatedAtUtc,
                UpdatedAtUtc = order.UpdatedAtUtc,
                ItemName = item?.Name,
                CurrentItemPrice = item?.Price
            };
        }
    }
}
