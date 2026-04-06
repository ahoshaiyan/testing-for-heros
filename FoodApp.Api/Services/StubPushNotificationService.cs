namespace FoodApp.Api.Services;

// No-op implementation used in development. In production, replace with a real
// Web Push / FCM implementation.
public class StubPushNotificationService : IPushNotificationService
{
    private readonly ILogger<StubPushNotificationService> _logger;

    public StubPushNotificationService(ILogger<StubPushNotificationService> logger)
    {
        _logger = logger;
    }

    public Task NotifyRestaurantAsync(Guid restaurantId, string title, string body)
    {
        _logger.LogInformation("[Push] Restaurant {RestaurantId}: {Title} — {Body}", restaurantId, title, body);
        return Task.CompletedTask;
    }

    public Task NotifyUserAsync(Guid userId, string title, string body)
    {
        _logger.LogInformation("[Push] User {UserId}: {Title} — {Body}", userId, title, body);
        return Task.CompletedTask;
    }
}
