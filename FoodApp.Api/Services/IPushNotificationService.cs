namespace FoodApp.Api.Services;

public interface IPushNotificationService
{
    Task NotifyRestaurantAsync(Guid restaurantId, string title, string body);
    Task NotifyUserAsync(Guid userId, string title, string body);
}
