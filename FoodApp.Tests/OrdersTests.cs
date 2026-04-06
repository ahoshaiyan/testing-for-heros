using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FoodApp.Api.Dtos;
using FoodApp.Api.Models;
using FoodApp.Tests.Infrastructure;

namespace FoodApp.Tests;

public class OrdersTests : IClassFixture<AppFactory>, IAsyncLifetime
{
    private readonly AppFactory _factory;

    public OrdersTests(AppFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.InitializeDatabaseAsync();
        _factory.PushNotifications.Clear();
        _factory.DriverSearch.Clear();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ListOrders_ReturnsOnlyOrdersForAuthenticatedUser()
    {
        using var db = _factory.CreateDbContext();

        var userA = await DatabaseSeeder.SeedUserAsync(db);
        var userB = await DatabaseSeeder.SeedUserAsync(db);
        var restaurant = await DatabaseSeeder.SeedRestaurantAsync(db);
        var item = await DatabaseSeeder.SeedMenuItemAsync(db, restaurantId: restaurant.Id);

        var clientA = ClientFor(userA);
        var clientB = ClientFor(userB);

        // Place one order as user A and one as user B using the real API.
        await clientA.PostAsJsonAsync("/api/orders", new PlaceOrderRequest(
            restaurant.Id, [new PlaceOrderItemDto(item.Id, 1)], null));

        await clientB.PostAsJsonAsync("/api/orders", new PlaceOrderRequest(
            restaurant.Id, [new PlaceOrderItemDto(item.Id, 1)], null));

        var response = await clientA.GetAsync("/api/orders");
        response.EnsureSuccessStatusCode();

        var orders = await response.Content.ReadFromJsonAsync<List<OrderDto>>();
        Assert.NotNull(orders);
        Assert.All(orders, o => Assert.Equal(userA.Id, o.UserId));
    }

    [Fact]
    public async Task ListOrders_Returns401_ForUnauthenticatedRequest()
    {
        var response = await _factory.CreateClient().GetAsync("/api/orders");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetOrderDetail_ReturnsCorrectOrderWithItems()
    {
        using var db = _factory.CreateDbContext();

        var user = await DatabaseSeeder.SeedUserAsync(db);
        var restaurant = await DatabaseSeeder.SeedRestaurantAsync(db);
        var item = await DatabaseSeeder.SeedMenuItemAsync(db, restaurantId: restaurant.Id, price: 8.50m);

        var client = ClientFor(user);

        var postResponse = await client.PostAsJsonAsync("/api/orders", new PlaceOrderRequest(
            restaurant.Id, [new PlaceOrderItemDto(item.Id, 3)], "Leave at door"));
        postResponse.EnsureSuccessStatusCode();

        var created = await postResponse.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(created);

        var detailResponse = await client.GetAsync($"/api/orders/{created.Id}");
        detailResponse.EnsureSuccessStatusCode();

        var detail = await detailResponse.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(detail);
        Assert.Equal(created.Id, detail.Id);
        Assert.Equal("Leave at door", detail.MessageToDriver);
        Assert.Equal(3, detail.Items.First().Quantity);
        Assert.Equal(8.50m, detail.Items.First().UnitPrice);
    }

    [Fact]
    public async Task GetOrderDetail_Returns404_ForUnknownId()
    {
        using var db = _factory.CreateDbContext();
        var user = await DatabaseSeeder.SeedUserAsync(db);

        var response = await ClientFor(user).GetAsync($"/api/orders/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private HttpClient ClientFor(User user)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _factory.CreateToken(user));
        return client;
    }
}
