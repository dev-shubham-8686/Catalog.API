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
                ItemId = order.ItemId,
                Quantity = order.Quantity,
                UnitPriceSnapshot = order.UnitPriceSnapshot,
                Status = order.Status,
                CancellationReason = order.CancellationReason,
                CreatedAtUtc = order.CreatedAtUtc,
                UpdatedAtUtc = order.UpdatedAtUtc
            };
        }
    }
}
