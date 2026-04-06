# Testing for Heroes

---

## Integration Tests > Unit Tests

Unit tests verify a piece of code in isolation. Integration tests verify that all pieces work together correctly.

When you write unit tests for business code you end up mocking the surrounding context — the database, the cache, external services. Your code passes every test, but you have only proven that it works in the world you imagined, not the world it will run in.

**A passing unit test suite is not a guarantee that your application works.**

Integration tests hit two birds with one stone: they confirm every code path executes and they confirm that the business logic produces the right result in a real environment.

---

## Small Tests Inside a Large Feature

A large business feature spans many steps, many branches, and many rules. Cramming all of that into one test produces a test that is long, hard to read, and tells you nothing useful when it fails.

The better approach: one test class per feature, one test method per assertion. Every test is small. Every test has one job.

---

## Write Stories, Not Tests

Your test class should read like a story. It should walk through the business process from start to end — the happy path, the rejection branch, the failure branch — with each test asserting one specific aspect of what should happen.

The story is self-documenting. A new engineer reading the test class understands the business rules without reading any other code.

---

## Example: Create Order

Here is the full story for our **Create Order** feature, taken directly from `CreateOrderTest.cs`.

### Guard Rails — Account State

```csharp
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
```

### Validation

```csharp
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
```

> Testing validations is not just testing the validation library — it is asserting a business rule. The 2000-character limit exists because the SMS provider enforces it. A coverage tool will not catch a missing or wrong limit. Only an explicit test will.

### Restaurant State

```csharp
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

    var request = new PlaceOrderRequest(closedRestaurant.Id, [new PlaceOrderItemDto(item.Id, 1)], null);
    var response = await ClientFor(_activeUser).PostAsJsonAsync("/api/orders", request);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

    var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
    Assert.Equal("Restaurant is closed", body!.Message);
}
```

### Happy Path — the Root of the Downstream Story

```csharp
[Fact]
public async Task CreateOrder_CreatesOrder_GivenCorrectAccountStateAndInput()
{
    var response = await ClientFor(_activeUser).PostAsJsonAsync("/api/orders", ValidRequest());

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);

    _createdOrder = await response.Content.ReadFromJsonAsync<OrderDto>();
    Assert.NotNull(_createdOrder);
}
```

Everything that follows depends on a successful order existing. Instead of constructing fake order state manually, each downstream test calls this method directly. The database contains exactly what the real business logic produces.

```csharp
[Fact]
public async Task SuccessfulOrder_HasStatusSetToWaitingForRestaurant()
{
    await CreateOrder_CreatesOrder_GivenCorrectAccountStateAndInput();

    Assert.Equal(OrderStatus.WaitingForRestaurant, _createdOrder!.Status);
}

[Fact]
public async Task SuccessfulOrder_HasAuthenticatedUserIdAttached()
{
    await CreateOrder_CreatesOrder_GivenCorrectAccountStateAndInput();

    Assert.Equal(_activeUser.Id, _createdOrder!.UserId);
}

[Fact]
public async Task SuccessfulOrder_HasMessageToDriverAttached()
{
    await CreateOrder_CreatesOrder_GivenCorrectAccountStateAndInput();

    Assert.Equal("Please ring the bell", _createdOrder!.MessageToDriver);
}

[Fact]
public async Task SuccessfulOrder_SendsNotificationToRestaurant()
{
    await CreateOrder_CreatesOrder_GivenCorrectAccountStateAndInput();

    Assert.Contains(
        _factory.PushNotifications.RestaurantNotifications,
        n => n.RestaurantId == _restaurant.Id);
}
```

### Branch: Restaurant Rejects the Order

```csharp
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
```

### Branch: Restaurant Accepts the Order

```csharp
[Fact]
public async Task AcceptedOrder_HasStatusSetToPreparing()
{
    await Restaurant_CanAcceptOrder();

    var order = await GetOrder(_createdOrder!.Id);
    Assert.Equal(OrderStatus.Preparing, order.Status);
}

[Fact]
public async Task AcceptedOrder_DispatchesDriverSearch()
{
    await Restaurant_CanAcceptOrder();

    Assert.Contains(_factory.DriverSearch.DispatchedOrderIds, id => id == _createdOrder!.Id);
}
```

---

## Why Calling an Earlier Test is Not an Anti-Pattern

Each test method in xUnit runs against a fresh class instance with a fresh database. When `RejectedOrder_HasStatusSetToRejected` calls `Restaurant_CanRejectOrder_WithAValidReason`, it is not reading state left by a previous test run. It is replaying the real business logic on the current instance to advance the pipeline to the correct stage before asserting.

This is correct and intentional:

- Every test is fully isolated and can run in any order or in parallel.
- Every downstream test is built on state that the real business code produced, not on state you manually constructed.
- If the order creation step is broken, all downstream tests fail — which is the right signal. The pipeline is broken at its root.

The alternative — manually setting `order.Status = Rejected` in the test setup — would let tests pass against a state that the application can never produce in production.

---

## Test for Your Business, Not for Coverage

100% test coverage does not mean 100% of your business logic is tested. A coverage tool measures which lines executed. It does not know whether the output was correct.

Consider this requirement:

> Message to delivery driver must not exceed 2000 characters.

You could achieve 100% line coverage on the Create Order endpoint without a single test that passes a 2001-character message. The coverage report would be green. The bug would be in production.

Your goal is to cover all business rules first, then worry about coverage as a secondary metric.

---

## Do Not Mock What You Can Run Locally

Mocking should be a last resort, used only for services you genuinely cannot replicate in a test environment.

**Mock:** SMS gateways, browser push notification endpoints, third-party payment processors, real-time exchange rate APIs.

**Do not mock:** PostgreSQL, Redis, S3-compatible storage. Since 2015 you can start all of these in Docker in a fraction of a second.

When you mock your database, you test against the contract you imagined. When you use a real database, you test against the contract that exists. This matters especially for database-specific features.

### Example: PostGIS Distance Ordering

This test only passes if PostGIS is actually executing the `ST_Distance` function. A mock or an in-memory database would never catch a misconfigured query, a wrong SRID, or a coordinate axis swap.

```csharp
[Fact]
public async Task ListRestaurants_WithCoordinates_ReturnsInDistanceOrder()
{
    using var db = _factory.CreateDbContext();

    var restaurantClose  = await DatabaseSeeder.SeedRestaurantAsync(db,
        name: "Restaurant Close",  lat: 24.7200, lng: 46.6800);  // ~0.9 km

    var restaurantFar    = await DatabaseSeeder.SeedRestaurantAsync(db,
        name: "Restaurant Far",    lat: 24.5500, lng: 46.6753);  // ~18 km

    var restaurantMedium = await DatabaseSeeder.SeedRestaurantAsync(db,
        name: "Restaurant Medium", lat: 24.6700, lng: 46.6753);  // ~4.8 km

    var response = await _client.GetAsync("/api/restaurants?lat=24.7136&lng=46.6753");
    response.EnsureSuccessStatusCode();

    var restaurants = await response.Content.ReadFromJsonAsync<List<RestaurantListItemDto>>();

    var seededIds = new[] { restaurantClose.Id, restaurantMedium.Id, restaurantFar.Id };
    var filtered = restaurants!.Where(r => seededIds.Contains(r.Id)).ToList();

    var closeIdx  = filtered.FindIndex(r => r.Id == restaurantClose.Id);
    var mediumIdx = filtered.FindIndex(r => r.Id == restaurantMedium.Id);
    var farIdx    = filtered.FindIndex(r => r.Id == restaurantFar.Id);

    Assert.True(closeIdx < mediumIdx);
    Assert.True(mediumIdx < farIdx);
}
```

### How the Test Infrastructure Starts Real Services

```csharp
public class AppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgis/postgis:16-3.4")
        .Build();

    private readonly MinioContainer _minio = new MinioBuilder().Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await _minio.StartAsync();
    }
}
```

The containers start once per test class, run real queries against a real database, and are torn down when the tests finish. No mocking. No in-memory substitutes.

### What We Do Mock — and Why

Push notifications and driver dispatch are external services with no local equivalent. We replace them with capturing stubs that record what was called so tests can assert on the side effects.

```csharp
// Replaces the real push notification service in tests.
// Push notifications go to an external browser push endpoint
// that cannot be replicated locally — the correct place for a stub.
public class CapturingPushNotificationService : IPushNotificationService
{
    public List<(Guid RestaurantId, string Title, string Body)> RestaurantNotifications { get; } = new();
    public List<(Guid UserId, string Title, string Body)> UserNotifications { get; } = new();

    public Task NotifyRestaurantAsync(Guid restaurantId, string title, string body)
    {
        RestaurantNotifications.Add((restaurantId, title, body));
        return Task.CompletedTask;
    }

    public Task NotifyUserAsync(Guid userId, string title, string body)
    {
        UserNotifications.Add((userId, title, body));
        return Task.CompletedTask;
    }
}
```

---

## Use Factories

Fixtures seed the entire database before any test runs. This leads to slow startup, hidden dependencies between tests, and data conflicts when two tests assume different states for the same row.

Factories create exactly the data each test needs, at the moment the test starts. The test framework rolls back the transaction when the test finishes. Each test is clean.

```csharp
public async Task InitializeAsync()
{
    await _factory.InitializeDatabaseAsync();

    using var db = _factory.CreateDbContext();

    _activeUser    = await DatabaseSeeder.SeedUserAsync(db, isEmailVerified: true,  isBanned: false);
    _bannedUser    = await DatabaseSeeder.SeedUserAsync(db, isEmailVerified: true,  isBanned: true);
    _unverifiedUser = await DatabaseSeeder.SeedUserAsync(db, isEmailVerified: false, isBanned: false);

    _restaurant = await DatabaseSeeder.SeedRestaurantAsync(db, isOpen: true, isBusy: false);
    _menuItem   = await DatabaseSeeder.SeedMenuItemAsync(db, restaurantId: _restaurant.Id, price: 20.00m);
}
```

Each seeder method creates one real record in the database with sensible defaults that can be overridden by the test. No YAML files. No shared global state.

```csharp
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
```
