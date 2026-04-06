using FoodApp.Api.Data;
using FoodApp.Api.Models;
using NetTopologySuite.Geometries;

namespace FoodApp.Tests.Infrastructure;

public static class DatabaseSeeder
{
    public static async Task<User> SeedUserAsync(
        AppDbContext db,
        bool isEmailVerified = true,
        bool isBanned = false,
        string? email = null)
    {
        var user = new User
        {
            Email = email ?? $"{Guid.NewGuid()}@test.com",
            IsEmailVerified = isEmailVerified,
            IsBanned = isBanned,
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public static async Task<Restaurant> SeedRestaurantAsync(
        AppDbContext db,
        string name = "Test Restaurant",
        string description = "A great place to eat",
        bool isOpen = true,
        bool isBusy = false,
        double? lat = null,
        double? lng = null)
    {
        Point? location = null;
        if (lat.HasValue && lng.HasValue)
        {
            location = new Point(lng.Value, lat.Value) { SRID = 4326 };
        }

        var restaurant = new Restaurant
        {
            Name = name,
            Description = description,
            IsOpen = isOpen,
            IsBusy = isBusy,
            Location = location
        };

        db.Restaurants.Add(restaurant);
        await db.SaveChangesAsync();
        return restaurant;
    }

    public static async Task<MenuItem> SeedMenuItemAsync(
        AppDbContext db,
        Guid restaurantId,
        string name = "Test Item",
        string description = "Delicious",
        decimal price = 9.99m,
        int sortOrder = 0)
    {
        var item = new MenuItem
        {
            RestaurantId = restaurantId,
            Name = name,
            Description = description,
            Price = price,
            SortOrder = sortOrder
        };

        db.MenuItems.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    public static async Task<MenuItemImage> SeedMenuItemImageAsync(
        AppDbContext db,
        Guid menuItemId,
        string url = "https://example.com/image.jpg",
        int sortOrder = 0)
    {
        var image = new MenuItemImage
        {
            MenuItemId = menuItemId,
            Url = url,
            SortOrder = sortOrder
        };

        db.MenuItemImages.Add(image);
        await db.SaveChangesAsync();
        return image;
    }
}
