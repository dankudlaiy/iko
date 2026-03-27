namespace iko_host.Models;

public class OAuthTokenResponse
{
    public string access_token { get; set; } = string.Empty;
    public string token_type { get; set; } = string.Empty;
    public string? scope { get; set; }
    public int expires_in { get; set; }
    public string? refresh_token { get; set; }
}
