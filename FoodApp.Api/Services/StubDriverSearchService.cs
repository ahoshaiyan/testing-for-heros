namespace FoodApp.Api.Services;

// No-op implementation used in development. In production, replace with a real
// driver-matching dispatch (e.g. a background job or third-party API).
public class StubDriverSearchService : IDriverSearchService
{
    private readonly ILogger<StubDriverSearchService> _logger;

    public StubDriverSearchService(ILogger<StubDriverSearchService> logger)
    {
        _logger = logger;
    }

    public Task DispatchAsync(Guid orderId)
    {
        _logger.LogInformation("[DriverSearch] Dispatching search for order {OrderId}", orderId);
        return Task.CompletedTask;
    }
}
