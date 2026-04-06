namespace FoodApp.Api.Models;

public class MenuItemImage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MenuItemId { get; set; }
    public string Url { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public MenuItem MenuItem { get; set; } = null!;
}
