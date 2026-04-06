using FoodApp.Api.Services;

namespace FoodApp.Tests.Infrastructure;

// Replaces the real push notification service in tests. Push notifications go
// to an external browser push endpoint that cannot be replicated locally — this
// is the correct place to use a capturing stub rather than a real service.
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

    public void Clear()
    {
        RestaurantNotifications.Clear();
        UserNotifications.Clear();
    }
}
