namespace FoodApp.Api.Models;

public enum OrderStatus
{
    WaitingForRestaurant,
    Rejected,
    Preparing,
    ReadyForPickup,
    Delivered,
    Cancelled
}

public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid RestaurantId { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.WaitingForRestaurant;
    public string? MessageToDriver { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Restaurant Restaurant { get; set; } = null!;
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
