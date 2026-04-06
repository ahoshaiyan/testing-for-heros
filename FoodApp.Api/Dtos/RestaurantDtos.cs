namespace FoodApp.Api.Dtos;

public record RestaurantListItemDto(
    Guid Id,
    string Name,
    string Description,
    string? IconUrl,
    string? BannerImageUrl,
    double? DistanceKm
);

public record MenuItemImageDto(
    Guid Id,
    string Url,
    int SortOrder
);

public record MenuItemDto(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    int SortOrder,
    IEnumerable<MenuItemImageDto> Images
);

public record RestaurantDetailDto(
    Guid Id,
    string Name,
    string Description,
    string? IconUrl,
    string? BannerImageUrl,
    IEnumerable<MenuItemDto> MenuItems
);
