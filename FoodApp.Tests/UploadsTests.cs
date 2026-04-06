using System.Net;
using System.Net.Http.Json;
using FoodApp.Tests.Infrastructure;
using Xunit;

namespace FoodApp.Tests;

public class UploadsTests : IClassFixture<AppFactory>, IAsyncLifetime
{
    private readonly AppFactory _factory;
    private readonly HttpClient _client;

    public UploadsTests(AppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.InitializeDatabaseAsync();
        await _factory.InitializeMinioAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task UploadImage_ReturnsPublicUrl()
    {
        // Create a minimal valid 1x1 PNG file in memory
        var pngBytes = CreateMinimalPng();

        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(pngBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "test.png");

        var response = await _client.PostAsync("/api/uploads", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<UploadResult>();
        Assert.NotNull(result);
        Assert.NotNull(result.Url);
        Assert.NotEmpty(result.Url);
        Assert.Contains("foodapp", result.Url);
        Assert.Contains("uploads/", result.Url);
        Assert.EndsWith(".png", result.Url);
    }

    [Fact]
    public async Task UploadImage_FileIsAccessibleViaHttpGet()
    {
        var pngBytes = CreateMinimalPng();

        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(pngBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "accessible.png");

        var uploadResponse = await _client.PostAsync("/api/uploads", content);
        uploadResponse.EnsureSuccessStatusCode();

        var result = await uploadResponse.Content.ReadFromJsonAsync<UploadResult>();
        Assert.NotNull(result);
        Assert.NotNull(result.Url);

        // Access the uploaded file via HTTP GET
        using var httpClient = new HttpClient();
        var getResponse = await httpClient.GetAsync(result.Url);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var downloadedBytes = await getResponse.Content.ReadAsByteArrayAsync();
        Assert.Equal(pngBytes.Length, downloadedBytes.Length);
    }

    [Fact]
    public async Task UploadWithNoFile_ReturnsBadRequest()
    {
        using var content = new MultipartFormDataContent();
        var response = await _client.PostAsync("/api/uploads", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadNonImage_ReturnsBadRequest()
    {
        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("not an image"));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", "test.txt");

        var response = await _client.PostAsync("/api/uploads", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static byte[] CreateMinimalPng()
    {
        // Minimal valid 1x1 transparent PNG
        return Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg=="
        );
    }

    private record UploadResult(string Url);
}
