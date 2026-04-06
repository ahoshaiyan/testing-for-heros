namespace FoodApp.Api.Services;

public interface IDriverSearchService
{
    Task DispatchAsync(Guid orderId);
}
