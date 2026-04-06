using FoodApp.Api.Data;
using FoodApp.Api.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace FoodApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RestaurantsController : ControllerBase
{
    private readonly AppDbContext _db;

    public RestaurantsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] double? lat,
        [FromQuery] double? lng,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        Point? userPoint = null;
        if (lat.HasValue && lng.HasValue)
        {
            userPoint = new Point(lng.Value, lat.Value) { SRID = 4326 };
        }

        IQueryable<Models.Restaurant> query = _db.Restaurants;

        if (userPoint != null)
        {
            query = query.OrderBy(r => r.Location == null
                ? double.MaxValue
                : r.Location.Distance(userPoint));
        }
        else
        {
            query = query.OrderBy(r => r.Name);
        }

        var restaurants = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var result = restaurants.Select(r =>
        {
            double? distanceKm = null;
            if (userPoint != null && r.Location != null)
            {
                // Distance() in NTS with geography gives meters; use manual haversine via ST_Distance
                // The value from EF/PostGIS Distance() on geography is in meters
                distanceKm = r.Location.Distance(userPoint) / 1000.0;
            }
            return new RestaurantListItemDto(r.Id, r.Name, r.Description, r.IconUrl, r.BannerImageUrl, distanceKm);
        });

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id)
    {
        var restaurant = await _db.Restaurants
            .Include(r => r.MenuItems.OrderBy(m => m.SortOrder))
            .ThenInclude(m => m.Images.OrderBy(i => i.SortOrder))
            .FirstOrDefaultAsync(r => r.Id == id);

        if (restaurant == null)
            return NotFound();

        var dto = new RestaurantDetailDto(
            restaurant.Id,
            restaurant.Name,
            restaurant.Description,
            restaurant.IconUrl,
            restaurant.BannerImageUrl,
            restaurant.MenuItems.Select(m => new MenuItemDto(
                m.Id,
                m.Name,
                m.Description,
                m.Price,
                m.SortOrder,
                m.Images.Select(i => new MenuItemImageDto(i.Id, i.Url, i.SortOrder))
            ))
        );

        return Ok(dto);
    }
}
