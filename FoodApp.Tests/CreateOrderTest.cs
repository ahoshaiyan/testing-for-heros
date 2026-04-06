using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FoodApp.Api.Dtos;
using FoodApp.Api.Models;
using FoodApp.Tests.Infrastructure;

namespace FoodApp.Tests;

/// <summary>
/// Tests the full Create Order business process as a story, from placing an order
/// through restaurant acceptance or rejection. Each test covers a single aspect of
/// the process. Tests that verify a later stage call the earlier stage's test method
/// directly to build up the correct real state — no hand-crafted intermediate state.
/// </summary>
public class CreateOrderTest : IClassFixture<AppFactory>, IAsyncLifetime
{
    private readonly AppFactory _factory;
    private readonly HttpClient _unauthenticatedClient;

    private User _activeUser = null!;
    private User _bannedUser = null!;
    private User _unverifiedUser = null!;
    private Restaurant _restaurant = null!;
    private MenuItem _menuItem = null!;

    // State accumulated as tests invoke prior-stage test methods.
    private OrderDto? _createdOrder;

    public CreateOrderTest(AppFactory factory)
    {
        _factory = factory;
        _unauthenticatedClient = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.InitializeDatabaseAsync();

        // Clear capturing services so each test starts with a clean slate.
        _factory.PushNotifications.Clear();
        _factory.DriverSearch.Clear();

        using var db = _factory.CreateDbContext();

        _activeUser = await DatabaseSeeder.SeedUserAsync(db, isEmailVerified: true, isBanned: false);
        _bannedUser = await DatabaseSeeder.SeedUserAsync(db, isEmailVerified: true, isBanned: true);
        _unverifiedUser = await DatabaseSeeder.SeedUserAsync(db, isEmailVerified: false, isBanned: false);

        _restaurant = await DatabaseSeeder.SeedRestaurantAsync(db, isOpen: true, isBusy: false);
        _menuItem = await DatabaseSeeder.SeedMenuItemAsync(db, restaurantId: _restaurant.Id, price: 20.00m);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // -------------------------------------------------------------------------
    // Guard rails — wrong account state or unauthenticated access
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateOrder_Returns401_ForUnauthenticatedRequests()
    {
        var response = await _unauthenticatedClient.PostAsJsonAsync("/api/orders", ValidRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_Returns403_ForBannedUsers()
    {
        var response = await ClientFor(_bannedUser).PostAsJsonAsync("/api/orders", ValidRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("you are not allowed to use the service, please contact customer support.", body!.Message);
    }

    [Fact]
    public async Task CreateOrder_Returns400_ForUnverifiedEmail()
    {
        var response = await ClientFor(_unverifiedUser).PostAsJsonAsync("/api/orders", ValidRequest());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("your email address is not verified, please verify it before attempting to order", body!.Message);
    }

    // -------------------------------------------------------------------------
    // Validation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateOrder_Returns422_WhenMessageToDriverExceeds2000Chars()
    {
        var request = ValidRequest() with { MessageToDriver = new string('x', 2001) };
        var response = await ClientFor(_activeUser).PostAsJsonAsync("/api/orders", request);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_Accepts_WhenMessageToDriverIsExactly2000Chars()
    {
        var request = ValidRequest() with { MessageToDriver = new string('x', 2000) };
        var response = await ClientFor(_activeUser).PostAsJsonAsync("/api/orders", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // Restaurant state
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateOrder_Returns400_ForInvalidRestaurantId()
    {
        var request = ValidRequest() with { RestaurantId = Guid.NewGuid() };
        var response = await ClientFor(_activeUser).PostAsJsonAsync("/api/orders", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("Invalid restaurant ID", body!.Message);
    }

    [Fact]
    public async Task CreateOrder_Returns400_WhenRestaurantIsClosed()
    {
        using var db = _factory.CreateDbContext();
        var closedRestaurant = await DatabaseSeeder.SeedRestaurantAsync(db, isOpen: false);
        var item = await DatabaseSeeder.SeedMenuItemAsync(db, restaurantId: closedRestaurant.Id);

        var request = new PlaceOrderRequest(
            closedRestaurant.Id,
            new[] { new PlaceOrderItemDto(item.Id, 1) },
            null);

        var response = await ClientFor(_activeUser).PostAsJsonAsync("/api/orders", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("Restaurant is closed", body!.Message);
    }

    [Fact]
    public async Task CreateOrder_Returns400_WhenRestaurantIsBusy()
    {
        using var db = _factory.CreateDbContext();
        var busyRestaurant = await DatabaseSeeder.SeedRestaurantAsync(db, isBusy: true);
        var item = await DatabaseSeeder.SeedMenuItemAsync(db, restaurantId: busyRestaurant.Id);

        var request = new PlaceOrderRequest(
            busyRestaurant.Id,
            new[] { new PlaceOrderItemDto(item.Id, 1) },
            null);

        var response = await ClientFor(_activeUser).PostAsJsonAsync("/api/orders", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("Restaurant is currently busy and cannot accept additional orders", body!.Message);
    }

    // -------------------------------------------------------------------------
    // Successful order — the root of the downstream story
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateOrder_CreatesOrder_GivenCorrectAccountStateAndInput()
    {
        var response = await ClientFor(_activeUser).PostAsJsonAsync("/api/orders", ValidRequest());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        _createdOrder = await response.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(_createdOrder);
    }

    [Fact]
    public async Task SuccessfulOrder_HasOrderRecordInDatabase()
    {
        await CreateOrder_CreatesOrder_GivenCorrectAccountStateAndInput();

        using var db = _factory.CreateDbContext();
        var order = await db.Orders.FindAsync(_createdOrder!.Id);

        Assert.NotNull(order);
    }

    [Fact]
    public async Task SuccessfulOrder_HasAuthenticatedUserIdAttached()
    {
        await CreateOrder_CreatesOrder_GivenCorrectAccountStateAndInput();

        Assert.Equal(_activeUser.Id, _createdOrder!.UserId);
    }

    [Fact]
    public async Task SuccessfulOrder_HasRestaurantIdAttached()
    {
        await CreateOrder_CreatesOrder_GivenCorrectAccountStateAndInput();

        Assert.Equal(_restaurant.Id, _createdOrder!.RestaurantId);
    }

    [Fact]
    public async Task SuccessfulOrder_HasMessageToDriverAttached()
    {
        await CreateOrder_CreatesOrder_GivenCorrectAccountStateAndInput();

        Assert.Equal("Please ring the bell", _createdOrder!.MessageToDriver);
    }

    [Fact]
    public async Task SuccessfulOrder_HasStatusSetToWaitingForRestaurant()
    {
        await CreateOrder_CreatesOrder_GivenCorrectAccountStateAndInput();

        Assert.Equal(OrderStatus.WaitingForRestaurant, _createdOrder!.Status);
    }

    [Fact]
    public async Task SuccessfulOrder_SendsNotificationToRestaurant()
    {
        await CreateOrder_CreatesOrder_GivenCorrectAccountStateAndInput();

        Assert.Contains(
            _factory.PushNotifications.RestaurantNotifications,
            n => n.RestaurantId == _restaurant.Id);
    }

    // -------------------------------------------------------------------------
    // Branch: restaurant rejects the order
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Restaurant_CanRejectOrder_WithAValidReason()
    {
        await CreateOrder_CreatesOrder_GivenCorrectAccountStateAndInput();

        var response = await _unauthenticatedClient.PutAsJsonAsync(
            $"/api/orders/{_createdOrder!.Id}/reject",
            new RejectOrderRequest("Out of ingredients"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RejectedOrder_HasStatusSetToRejected()
    {
        await Restaurant_CanRejectOrder_WithAValidReason();

        var order = await GetOrder(_createdOrder!.Id);
        Assert.Equal(OrderStatus.Rejected, order.Status);
    }

    [Fact]
    public async Task RejectedOrder_HasTheSameReasonSetByTheRestaurant()
    {
        await Restaurant_CanRejectOrder_WithAValidReason();

        var order = await GetOrder(_createdOrder!.Id);
        Assert.Equal("Out of ingredients", order.RejectionReason);
    }

    [Fact]
    public async Task RejectedOrder_SendsNotificationToTheUser()
    {
        await Restaurant_CanRejectOrder_WithAValidReason();

        Assert.Contains(
            _factory.PushNotifications.UserNotifications,
            n => n.UserId == _activeUser.Id);
    }

    // -------------------------------------------------------------------------
    // Branch: restaurant accepts the order
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Restaurant_CanAcceptOrder()
    {
        await CreateOrder_CreatesOrder_GivenCorrectAccountStateAndInput();

        var response = await _unauthenticatedClient.PutAsJsonAsync(
            $"/api/orders/{_createdOrder!.Id}/accept",
            new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AcceptedOrder_HasStatusSetToPreparing()
    {
        await Restaurant_CanAcceptOrder();

        var order = await GetOrder(_createdOrder!.Id);
        Assert.Equal(OrderStatus.Preparing, order.Status);
    }

    [Fact]
    public async Task AcceptedOrder_SendsNotificationToTheUser()
    {
        await Restaurant_CanAcceptOrder();

        Assert.Contains(
            _factory.PushNotifications.UserNotifications,
            n => n.UserId == _activeUser.Id);
    }

    [Fact]
    public async Task AcceptedOrder_DispatchesDriverSearch()
    {
        await Restaurant_CanAcceptOrder();

        Assert.Contains(_factory.DriverSearch.DispatchedOrderIds, id => id == _createdOrder!.Id);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private PlaceOrderRequest ValidRequest() => new(
        RestaurantId: _restaurant.Id,
        Items: [new PlaceOrderItemDto(_menuItem.Id, 2)],
        MessageToDriver: "Please ring the bell"
    );

    private HttpClient ClientFor(User user)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _factory.CreateToken(user));
        return client;
    }

    private async Task<OrderDto> GetOrder(Guid id)
    {
        var client = ClientFor(_activeUser);
        var response = await client.GetAsync($"/api/orders/{id}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderDto>())!;
    }
}
