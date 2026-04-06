using FoodApp.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FoodApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UploadsController : ControllerBase
{
    private readonly StorageService _storageService;

    public UploadsController(StorageService storageService)
    {
        _storageService = storageService;
    }

    [HttpPost]
    [RequestSizeLimit(20 * 1024 * 1024)] // 20 MB
    public async Task<IActionResult> Upload(IFormFile file, [FromQuery] string folder = "uploads")
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file provided");

        var allowedContentTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
        if (!allowedContentTypes.Contains(file.ContentType.ToLowerInvariant()))
            return BadRequest("Only image files are allowed");

        var url = await _storageService.UploadAsync(file, folder);
        return Ok(new { url });
    }
}
