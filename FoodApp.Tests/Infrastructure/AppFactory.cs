using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FoodApp.Api.Data;
using FoodApp.Api.Models;
using FoodApp.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Minio;
using Minio.DataModel.Args;
using Testcontainers.Minio;
using Testcontainers.PostgreSql;

namespace FoodApp.Tests.Infrastructure;

public class AppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Shared across all tests — push and driver are external services that
    // cannot be replicated locally, so we capture their calls in tests.
    public readonly CapturingPushNotificationService PushNotifications = new();
    public readonly CapturingDriverSearchService DriverSearch = new();

    // Fixed key used by both AppFactory (token generation) and the app (token validation).
    internal const string JwtKey = "test-secret-key-for-jwt-signing-32chars!";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgis/postgis:16-3.4")
        .Build();

    private readonly MinioContainer _minio = new MinioBuilder()
        .Build();

    public string MinioPublicBaseUrl { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await _minio.StartAsync();
        var minioUri = new Uri(_minio.GetConnectionString());
        MinioPublicBaseUrl = $"http://{minioUri.Host}:{minioUri.Port}";
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _minio.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var minioUri = new Uri(_minio.GetConnectionString());
        var minioHostPort = $"{minioUri.Host}:{minioUri.Port}";

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString(),
                ["Storage:Endpoint"] = minioHostPort,
                ["Storage:AccessKey"] = MinioBuilder.DefaultUsername,
                ["Storage:SecretKey"] = MinioBuilder.DefaultPassword,
                ["Storage:BucketName"] = "foodapp",
                ["Storage:PublicBaseUrl"] = MinioPublicBaseUrl,
                ["Jwt:Key"] = JwtKey,
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();

            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(
                    _postgres.GetConnectionString(),
                    npgsql => npgsql.UseNetTopologySuite()));

            services.RemoveAll<IMinioClient>();

            services.AddMinio(client =>
                client.WithEndpoint(minioHostPort)
                      .WithCredentials(MinioBuilder.DefaultUsername, MinioBuilder.DefaultPassword)
                      .WithSSL(false));

            // Replace external services with capturing stubs.
            // Push notifications and driver search call third-party APIs that
            // cannot be run locally — this is the right place to use stubs.
            services.RemoveAll<IPushNotificationService>();
            services.AddSingleton<IPushNotificationService>(PushNotifications);

            services.RemoveAll<IDriverSearchService>();
            services.AddSingleton<IDriverSearchService>(DriverSearch);

            // Program.cs reads Jwt:Key and captures it into IssuerSigningKey at service-
            // registration time, before ConfigureAppConfiguration overrides take effect.
            // PostConfigure runs after all Configure calls, so it always wins.
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters.IssuerSigningKey =
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
            });
        });

        builder.UseEnvironment("Testing");
    }

    public AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString(), npgsql => npgsql.UseNetTopologySuite())
            .Options;
        return new AppDbContext(options);
    }

    public async Task InitializeDatabaseAsync()
    {
        using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task InitializeMinioAsync()
    {
        var minioUri = new Uri(_minio.GetConnectionString());
        var client = new MinioClient()
            .WithEndpoint($"{minioUri.Host}:{minioUri.Port}")
            .WithCredentials(MinioBuilder.DefaultUsername, MinioBuilder.DefaultPassword)
            .WithSSL(false)
            .Build();

        const string bucket = "foodapp";

        if (!await client.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket)))
            await client.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket));

        var policy = """
            {
                "Version": "2012-10-17",
                "Statement": [{
                    "Effect": "Allow",
                    "Principal": {"AWS": ["*"]},
                    "Action": ["s3:GetObject"],
                    "Resource": ["arn:aws:s3:::foodapp/*"]
                }]
            }
            """;

        await client.SetPolicyAsync(new SetPolicyArgs().WithBucket(bucket).WithPolicy(policy));
    }

    /// <summary>
    /// Generates a valid JWT for the given user. Use this to create an authenticated
    /// HttpClient in tests without hitting a real auth endpoint.
    /// </summary>
    public string CreateToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            claims: [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
