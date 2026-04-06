using NetTopologySuite.Geometries;

namespace FoodApp.Api.Models;

public class Restaurant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
    public string? BannerImageUrl { get; set; }
    public Point? Location { get; set; }
    public bool IsOpen { get; set; } = true;
    public bool IsBusy { get; set; } = false;

    public ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
}
