using FoodApp.Api.Services;

namespace FoodApp.Tests.Infrastructure;

// Replaces the real driver search service in tests. Driver matching calls an
// external dispatch API that cannot be replicated locally — correct place for a stub.
public class CapturingDriverSearchService : IDriverSearchService
{
    public List<Guid> DispatchedOrderIds { get; } = new();

    public Task DispatchAsync(Guid orderId)
    {
        DispatchedOrderIds.Add(orderId);
        return Task.CompletedTask;
    }

    public void Clear() => DispatchedOrderIds.Clear();
}
