using System.Security.Claims;
using FoodApp.Api.Data;
using FoodApp.Api.Dtos;
using FoodApp.Api.Models;
using FoodApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPushNotificationService _pushNotifications;
    private readonly IDriverSearchService _driverSearch;

    public OrdersController(
        AppDbContext db,
        IPushNotificationService pushNotifications,
        IDriverSearchService driverSearch)
    {
        _db = db;
        _pushNotifications = pushNotifications;
        _driverSearch = driverSearch;
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> List()
    {
        var userId = CurrentUserId();

        var orders = await _db.Orders
            .Include(o => o.Restaurant)
            .Include(o => o.Items)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return Ok(orders.Select(MapToDto));
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Detail(Guid id)
    {
        var order = await _db.Orders
            .Include(o => o.Restaurant)
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
            return NotFound();

        return Ok(MapToDto(order));
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> PlaceOrder([FromBody] PlaceOrderRequest request)
    {
        var userId = CurrentUserId();

        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return Unauthorized();

        if (user.IsBanned)
            return StatusCode(403, new ErrorResponse("you are not allowed to use the service, please contact customer support."));

        if (!user.IsEmailVerified)
            return BadRequest(new ErrorResponse("your email address is not verified, please verify it before attempting to order"));

        if (request.MessageToDriver?.Length > 2000)
            return UnprocessableEntity(new ErrorResponse("message_to_driver cannot exceed 2000 characters"));

        if (!request.Items.Any())
            return BadRequest(new ErrorResponse("Order must contain at least one item"));

        var restaurant = await _db.Restaurants.FindAsync(request.RestaurantId);
        if (restaurant == null)
            return BadRequest(new ErrorResponse("Invalid restaurant ID"));

        if (!restaurant.IsOpen)
            return BadRequest(new ErrorResponse("Restaurant is closed"));

        if (restaurant.IsBusy)
            return BadRequest(new ErrorResponse("Restaurant is currently busy and cannot accept additional orders"));

        var menuItemIds = request.Items.Select(i => i.MenuItemId).ToList();
        var menuItems = await _db.MenuItems
            .Where(m => menuItemIds.Contains(m.Id) && m.RestaurantId == request.RestaurantId)
            .ToDictionaryAsync(m => m.Id);

        foreach (var item in request.Items)
        {
            if (!menuItems.ContainsKey(item.MenuItemId))
                return BadRequest(new ErrorResponse($"Menu item {item.MenuItemId} does not belong to this restaurant"));
            if (item.Quantity <= 0)
                return BadRequest(new ErrorResponse("Quantity must be positive"));
        }

        var order = new Order
        {
            UserId = userId,
            RestaurantId = request.RestaurantId,
            Status = OrderStatus.WaitingForRestaurant,
            MessageToDriver = request.MessageToDriver,
            Items = request.Items.Select(i => new OrderItem
            {
                MenuItemId = i.MenuItemId,
                Quantity = i.Quantity,
                UnitPrice = menuItems[i.MenuItemId].Price,
                ItemName = menuItems[i.MenuItemId].Name
            }).ToList()
        };

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        await _pushNotifications.NotifyRestaurantAsync(
            restaurant.Id,
            "New Order",
            $"You have a new order with {order.Items.Count} item(s).");

        order.Restaurant = restaurant;
        return CreatedAtAction(nameof(Detail), new { id = order.Id }, MapToDto(order));
    }

    [HttpPut("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectOrderRequest request)
    {
        var order = await _db.Orders.FindAsync(id);
        if (order == null)
            return NotFound();

        if (order.Status != OrderStatus.WaitingForRestaurant)
            return BadRequest(new ErrorResponse("Only orders waiting for restaurant can be rejected"));

        order.Status = OrderStatus.Rejected;
        order.RejectionReason = request.Reason;
        await _db.SaveChangesAsync();

        await _pushNotifications.NotifyUserAsync(
            order.UserId,
            "Order Rejected",
            $"Your order was rejected. Reason: {request.Reason}");

        return Ok();
    }

    [HttpPut("{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id)
    {
        var order = await _db.Orders.FindAsync(id);
        if (order == null)
            return NotFound();

        if (order.Status != OrderStatus.WaitingForRestaurant)
            return BadRequest(new ErrorResponse("Only orders waiting for restaurant can be accepted"));

        order.Status = OrderStatus.Preparing;
        await _db.SaveChangesAsync();

        await _pushNotifications.NotifyUserAsync(
            order.UserId,
            "Order Accepted",
            "Your order has been accepted and is being prepared.");

        await _driverSearch.DispatchAsync(order.Id);

        return Ok();
    }

    private Guid CurrentUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static OrderDto MapToDto(Order order) => new(
        order.Id,
        order.UserId,
        order.RestaurantId,
        order.Restaurant?.Name ?? string.Empty,
        order.Status,
        order.MessageToDriver,
        order.RejectionReason,
        order.CreatedAt,
        order.Items.Select(i => new OrderItemDto(i.Id, i.MenuItemId, i.ItemName, i.Quantity, i.UnitPrice))
    );
}
