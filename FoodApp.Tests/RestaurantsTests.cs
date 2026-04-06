using System.Net.Http.Json;
using FoodApp.Api.Dtos;
using FoodApp.Tests.Infrastructure;
using Xunit;

namespace FoodApp.Tests;

public class RestaurantsTests : IClassFixture<AppFactory>, IAsyncLifetime
{
    private readonly AppFactory _factory;
    private readonly HttpClient _client;

    public RestaurantsTests(AppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.InitializeDatabaseAsync();
        await _factory.InitializeMinioAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ListRestaurants_WithCoordinates_ReturnsInDistanceOrder()
    {
        // Seed 3 restaurants at known GPS coordinates
        // Reference point: Riyadh center ~(24.7136, 46.6753)
        // Restaurant A: close (~1 km away)
        // Restaurant B: medium (~5 km away)
        // Restaurant C: far (~20 km away)

        using var db = _factory.CreateDbContext();

        // Reference point: 24.7136, 46.6753
        var restaurantClose = await DatabaseSeeder.SeedRestaurantAsync(db,
            name: "Restaurant Close",
            description: "Very close",
            lat: 24.7200, lng: 46.6800);   // ~0.9 km from reference

        var restaurantFar = await DatabaseSeeder.SeedRestaurantAsync(db,
            name: "Restaurant Far",
            description: "Very far",
            lat: 24.5500, lng: 46.6753);   // ~18 km from reference

        var restaurantMedium = await DatabaseSeeder.SeedRestaurantAsync(db,
            name: "Restaurant Medium",
            description: "Medium distance",
            lat: 24.6700, lng: 46.6753);   // ~4.8 km from reference

        // Query with reference point
        var response = await _client.GetAsync("/api/restaurants?lat=24.7136&lng=46.6753");
        response.EnsureSuccessStatusCode();

        var restaurants = await response.Content.ReadFromJsonAsync<List<RestaurantListItemDto>>();
        Assert.NotNull(restaurants);

        // Filter to just the ones we seeded (other tests may have added data)
        var seededIds = new[] { restaurantClose.Id, restaurantMedium.Id, restaurantFar.Id };
        var filtered = restaurants.Where(r => seededIds.Contains(r.Id)).ToList();

        Assert.Equal(3, filtered.Count);

        // Assert order: close < medium < far
        var closeIdx = filtered.FindIndex(r => r.Id == restaurantClose.Id);
        var mediumIdx = filtered.FindIndex(r => r.Id == restaurantMedium.Id);
        var farIdx = filtered.FindIndex(r => r.Id == restaurantFar.Id);

        Assert.True(closeIdx < mediumIdx, "Close restaurant should appear before medium");
        Assert.True(mediumIdx < farIdx, "Medium restaurant should appear before far");

        // Verify distance values are populated and increasing
        var closeDto = filtered.First(r => r.Id == restaurantClose.Id);
        var mediumDto = filtered.First(r => r.Id == restaurantMedium.Id);
        var farDto = filtered.First(r => r.Id == restaurantFar.Id);

        Assert.NotNull(closeDto.DistanceKm);
        Assert.NotNull(mediumDto.DistanceKm);
        Assert.NotNull(farDto.DistanceKm);

        Assert.True(closeDto.DistanceKm < mediumDto.DistanceKm, "Close distance should be less than medium");
        Assert.True(mediumDto.DistanceKm < farDto.DistanceKm, "Medium distance should be less than far");
    }

    [Fact]
    public async Task ListRestaurants_WithoutCoordinates_ReturnsOrderedByName()
    {
        using var db = _factory.CreateDbContext();

        await DatabaseSeeder.SeedRestaurantAsync(db, name: "Zebra Cafe");
        await DatabaseSeeder.SeedRestaurantAsync(db, name: "Alpha Diner");

        var response = await _client.GetAsync("/api/restaurants");
        response.EnsureSuccessStatusCode();

        var restaurants = await response.Content.ReadFromJsonAsync<List<RestaurantListItemDto>>();
        Assert.NotNull(restaurants);

        // All distance values should be null
        Assert.All(restaurants, r => Assert.Null(r.DistanceKm));

        // Verify name ordering for the ones we seeded
        var names = restaurants.Select(r => r.Name).ToList();
        var alphaIdx = names.IndexOf("Alpha Diner");
        var zebraIdx = names.IndexOf("Zebra Cafe");
        Assert.True(alphaIdx < zebraIdx, "Alpha Diner should appear before Zebra Cafe");
    }

    [Fact]
    public async Task GetRestaurantDetail_ReturnsMenuItemsWithImages()
    {
        using var db = _factory.CreateDbContext();

        var restaurant = await DatabaseSeeder.SeedRestaurantAsync(db,
            name: "Detail Test Restaurant",
            description: "Testing detail endpoint");

        var item1 = await DatabaseSeeder.SeedMenuItemAsync(db,
            restaurantId: restaurant.Id,
            name: "Burger",
            description: "Juicy burger",
            price: 12.50m,
            sortOrder: 1);

        var item2 = await DatabaseSeeder.SeedMenuItemAsync(db,
            restaurantId: restaurant.Id,
            name: "Pizza",
            description: "Wood-fired pizza",
            price: 18.00m,
            sortOrder: 2);

        await DatabaseSeeder.SeedMenuItemImageAsync(db, item1.Id, "https://example.com/burger1.jpg", 1);
        await DatabaseSeeder.SeedMenuItemImageAsync(db, item1.Id, "https://example.com/burger2.jpg", 2);
        await DatabaseSeeder.SeedMenuItemImageAsync(db, item2.Id, "https://example.com/pizza.jpg", 1);

        var response = await _client.GetAsync($"/api/restaurants/{restaurant.Id}");
        response.EnsureSuccessStatusCode();

        var detail = await response.Content.ReadFromJsonAsync<RestaurantDetailDto>();
        Assert.NotNull(detail);
        Assert.Equal(restaurant.Id, detail.Id);
        Assert.Equal("Detail Test Restaurant", detail.Name);

        var menuItems = detail.MenuItems.ToList();
        Assert.Equal(2, menuItems.Count);

        var burger = menuItems.First(m => m.Name == "Burger");
        Assert.Equal(12.50m, burger.Price);
        Assert.Equal(2, burger.Images.Count());

        var pizza = menuItems.First(m => m.Name == "Pizza");
        Assert.Equal(18.00m, pizza.Price);
        Assert.Single(pizza.Images);
    }

    [Fact]
    public async Task GetRestaurantDetail_NotFound_Returns404()
    {
        var response = await _client.GetAsync($"/api/restaurants/{Guid.NewGuid()}");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }
}
