namespace FoodApp.Api.Models;

public class MenuItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RestaurantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int SortOrder { get; set; }

    public Restaurant Restaurant { get; set; } = null!;
    public ICollection<MenuItemImage> Images { get; set; } = new List<MenuItemImage>();
}
