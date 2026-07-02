namespace iko_host.Services;

using iko_host.Clients;
using iko_host.Data;
using iko_host.Models;

/// <summary>
/// Returns a usable access token for a connected account, transparently refreshing it
/// via the platform's refresh token when it has expired (Spotify, YouTube). Apple Music
/// uses a long-lived user token and is returned as-is.
/// </summary>
public class AccountTokenService
{
    private readonly AppDbContext _db;
    private readonly SpotifyClient _spotify;
    private readonly YouTubeClient _youtube;
    private readonly ILogger<AccountTokenService> _logger;

    public AccountTokenService(
        AppDbContext db, SpotifyClient spotify, YouTubeClient youtube, ILogger<AccountTokenService> logger)
    {
        _db = db;
        _spotify = spotify;
        _youtube = youtube;
        _logger = logger;
    }

    public async Task<string> GetValidAccessTokenAsync(ConnectedAccount account)
    {
        var needsRefresh = account.ExpiresAt.HasValue
            && account.ExpiresAt.Value <= DateTime.UtcNow.AddMinutes(1)
            && !string.IsNullOrEmpty(account.RefreshToken);

        if (!needsRefresh)
            return account.AccessToken;

        switch (account.Platform)
        {
            case Platform.Spotify:
                var sp = await _spotify.RefreshAccessToken(account.RefreshToken!);
                if (!string.IsNullOrEmpty(sp?.access_token))
                {
                    account.AccessToken = sp!.access_token;
                    account.ExpiresAt = DateTime.UtcNow.AddSeconds(sp.expires_in);
                    if (!string.IsNullOrEmpty(sp.refresh_token)) account.RefreshToken = sp.refresh_token;
                    await _db.SaveChangesAsync();
                }
                else _logger.LogWarning("Spotify token refresh failed for account {AccountId}", account.Id);
                break;

            case Platform.YouTube:
                var yt = await _youtube.RefreshAccessToken(account.RefreshToken!);
                if (!string.IsNullOrEmpty(yt?.access_token))
                {
                    account.AccessToken = yt!.access_token;
                    account.ExpiresAt = DateTime.UtcNow.AddSeconds(yt.expires_in);
                    if (!string.IsNullOrEmpty(yt.refresh_token)) account.RefreshToken = yt.refresh_token;
                    await _db.SaveChangesAsync();
                }
                else _logger.LogWarning("YouTube token refresh failed for account {AccountId}", account.Id);
                break;
        }

        return account.AccessToken;
    }
}
