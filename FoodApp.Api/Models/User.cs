namespace FoodApp.Api.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public bool IsEmailVerified { get; set; }
    public bool IsBanned { get; set; }
}
