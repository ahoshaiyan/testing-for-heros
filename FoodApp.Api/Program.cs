using System.Text;
using FoodApp.Api.Data;
using FoodApp.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Minio;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();

// PostgreSQL + PostGIS
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.UseNetTopologySuite()
    );
});

// MinIO
builder.Services.AddMinio(client =>
{
    client.WithEndpoint(builder.Configuration["Storage:Endpoint"] ?? "localhost:9000")
          .WithCredentials(
              builder.Configuration["Storage:AccessKey"] ?? "minioadmin",
              builder.Configuration["Storage:SecretKey"] ?? "minioadmin")
          .WithSSL(false);
});

builder.Services.AddScoped<StorageService>();
builder.Services.AddScoped<IPushNotificationService, StubPushNotificationService>();
builder.Services.AddScoped<IDriverSearchService, StubDriverSearchService>();

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "dev-secret-key-change-in-production-32chars";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    var storage = scope.ServiceProvider.GetRequiredService<StorageService>();
    await storage.EnsureBucketExistsAsync();
}

app.Run();

public partial class Program { }
