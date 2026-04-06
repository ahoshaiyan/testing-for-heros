namespace FoodApp.Api.Models;

public class OrderItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Guid MenuItemId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string ItemName { get; set; } = string.Empty;

    public Order Order { get; set; } = null!;
    public MenuItem MenuItem { get; set; } = null!;
}
