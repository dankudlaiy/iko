namespace iko_host.Models;

public class ConnectedAccount
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Platform Platform { get; set; }
    public string AccessToken { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? PlatformUserId { get; set; }
    public string? PlatformDisplayName { get; set; }

    public User User { get; set; } = null!;
}
