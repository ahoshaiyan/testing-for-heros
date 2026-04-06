using FoodApp.Api.Models;

namespace FoodApp.Api.Dtos;

public record PlaceOrderItemDto(
    Guid MenuItemId,
    int Quantity
);

public record PlaceOrderRequest(
    Guid RestaurantId,
    IEnumerable<PlaceOrderItemDto> Items,
    string? MessageToDriver
);

public record RejectOrderRequest(string Reason);

public record OrderItemDto(
    Guid Id,
    Guid MenuItemId,
    string ItemName,
    int Quantity,
    decimal UnitPrice
);

public record OrderDto(
    Guid Id,
    Guid UserId,
    Guid RestaurantId,
    string RestaurantName,
    OrderStatus Status,
    string? MessageToDriver,
    string? RejectionReason,
    DateTime CreatedAt,
    IEnumerable<OrderItemDto> Items
);

public record ErrorResponse(string Message);
