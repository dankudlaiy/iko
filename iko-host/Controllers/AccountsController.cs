using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using iko_host.Clients;
using iko_host.Data;
using iko_host.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace iko_host.Controllers;

[ApiController]
[Route("api/accounts")]
[Authorize]
public class AccountsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly SpotifyClient _spotifyClient;
    private readonly YouTubeClient _youTubeClient;
    private readonly AppleMusicClient _appleMusicClient;
    private readonly IConfiguration _config;

    public AccountsController(
        AppDbContext db,
        SpotifyClient spotifyClient,
        YouTubeClient youTubeClient,
        AppleMusicClient appleMusicClient,
        IConfiguration config)
    {
        _db = db;
        _spotifyClient = spotifyClient;
        _youTubeClient = youTubeClient;
        _appleMusicClient = appleMusicClient;
        _config = config;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> ListAccounts()
    {
        var userId = GetUserId();
        var accounts = await _db.ConnectedAccounts
            .Where(ca => ca.UserId == userId)
            .Select(ca => new
            {
                ca.Platform,
                ca.PlatformUserId,
                ca.PlatformDisplayName,
                ca.ExpiresAt
            })
            .ToListAsync();

        return Ok(new { data = accounts, error = (string?)null });
    }

    [HttpDelete("{platform}")]
    public async Task<IActionResult> Disconnect(Platform platform)
    {
        var userId = GetUserId();
        var account = await _db.ConnectedAccounts
            .FirstOrDefaultAsync(ca => ca.UserId == userId && ca.Platform == platform);

        if (account == null)
            return NotFound(new { data = (object?)null, error = "Account not connected" });

        _db.ConnectedAccounts.Remove(account);
        await _db.SaveChangesAsync();

        return Ok(new { data = true, error = (string?)null });
    }

    [HttpGet("token/{platform}")]
    public async Task<IActionResult> GetToken(Platform platform)
    {
        var userId = GetUserId();
        var account = await _db.ConnectedAccounts
            .FirstOrDefaultAsync(ca => ca.UserId == userId && ca.Platform == platform);

        if (account == null)
            return NotFound(new { data = (object?)null, error = "Account not connected" });

        if (account.ExpiresAt.HasValue && account.ExpiresAt.Value <= DateTime.UtcNow
            && !string.IsNullOrEmpty(account.RefreshToken))
        {
            switch (platform)
            {
                case Platform.Spotify:
                    var spotifyRefreshed = await _spotifyClient.RefreshAccessToken(account.RefreshToken);
                    if (spotifyRefreshed != null)
                    {
                        account.AccessToken = spotifyRefreshed.access_token;
                        account.ExpiresAt = DateTime.UtcNow.AddSeconds(spotifyRefreshed.expires_in);
                        if (!string.IsNullOrEmpty(spotifyRefreshed.refresh_token))
                            account.RefreshToken = spotifyRefreshed.refresh_token;
                        await _db.SaveChangesAsync();
                    }
                    break;

                case Platform.YouTube:
                    var ytRefreshed = await _youTubeClient.RefreshAccessToken(account.RefreshToken);
                    if (ytRefreshed != null)
                    {
                        account.AccessToken = ytRefreshed.access_token;
                        account.ExpiresAt = DateTime.UtcNow.AddSeconds(ytRefreshed.expires_in);
                        if (!string.IsNullOrEmpty(ytRefreshed.refresh_token))
                            account.RefreshToken = ytRefreshed.refresh_token;
                        await _db.SaveChangesAsync();
                    }
                    break;
            }
        }

        return Ok(new { data = new { accessToken = account.AccessToken }, error = (string?)null });
    }

    [HttpGet("refresh/{platform}")]
    public async Task<IActionResult> RefreshToken(Platform platform)
    {
        var userId = GetUserId();
        var account = await _db.ConnectedAccounts
            .FirstOrDefaultAsync(ca => ca.UserId == userId && ca.Platform == platform);

        if (account == null)
            return NotFound(new { data = (object?)null, error = "Account not connected" });

        if (string.IsNullOrEmpty(account.RefreshToken))
            return BadRequest(new { data = (object?)null, error = "No refresh token available" });

        switch (platform)
        {
            case Platform.Spotify:
                var spotifyRefreshed = await _spotifyClient.RefreshAccessToken(account.RefreshToken);
                if (spotifyRefreshed == null)
                    return StatusCode(500, new { data = (object?)null, error = "Refresh failed" });

                account.AccessToken = spotifyRefreshed.access_token;
                account.ExpiresAt = DateTime.UtcNow.AddSeconds(spotifyRefreshed.expires_in);
                if (!string.IsNullOrEmpty(spotifyRefreshed.refresh_token))
                    account.RefreshToken = spotifyRefreshed.refresh_token;
                await _db.SaveChangesAsync();

                return Ok(new { data = new { accessToken = account.AccessToken }, error = (string?)null });

            case Platform.YouTube:
                var ytRefreshed = await _youTubeClient.RefreshAccessToken(account.RefreshToken);
                if (ytRefreshed == null)
                    return StatusCode(500, new { data = (object?)null, error = "Refresh failed" });

                account.AccessToken = ytRefreshed.access_token;
                account.ExpiresAt = DateTime.UtcNow.AddSeconds(ytRefreshed.expires_in);
                if (!string.IsNullOrEmpty(ytRefreshed.refresh_token))
                    account.RefreshToken = ytRefreshed.refresh_token;
                await _db.SaveChangesAsync();

                return Ok(new { data = new { accessToken = account.AccessToken }, error = (string?)null });

            default:
                return BadRequest(new { data = (object?)null, error = "Refresh not supported for this platform" });
        }
    }

    // --- Spotify ---
    [HttpGet("connect/spotify")]
    public IActionResult ConnectSpotify()
    {
        var clientId = Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_ID") ?? "";
        var scopes = "user-read-private user-read-email playlist-modify-public playlist-modify-private streaming user-read-playback-state user-modify-playback-state";
        var redirectUri = SpotifyClient.RedirectUri;

        var jwt = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

        var url = $"https://accounts.spotify.com/authorize?client_id={clientId}" +
                  $"&response_type=code&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                  $"&scope={Uri.EscapeDataString(scopes)}" +
                  $"&state={Uri.EscapeDataString(jwt)}";

        return Ok(new { data = new { url }, error = (string?)null });
    }

    [HttpGet("callback/spotify")]
    [AllowAnonymous]
    public async Task<IActionResult> CallbackSpotify([FromQuery] string code, [FromQuery] string? state)
    {
        var tokenResponse = await _spotifyClient.ObtainAccessToken(code);

        if (tokenResponse == null)
            return Redirect("http://localhost:4200/settings?error=spotify_auth_failed");

        if (!string.IsNullOrEmpty(state))
        {
            var userId = ExtractUserIdFromJwt(state);
            if (userId != null)
            {
                var (platformUserId, displayName) = await _spotifyClient.GetCurrentUser(tokenResponse.access_token);

                var existing = await _db.ConnectedAccounts
                    .FirstOrDefaultAsync(ca => ca.UserId == userId.Value && ca.Platform == Platform.Spotify);

                if (existing != null)
                {
                    existing.AccessToken = tokenResponse.access_token;
                    existing.RefreshToken = tokenResponse.refresh_token;
                    existing.ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in);
                    existing.PlatformUserId = platformUserId;
                    existing.PlatformDisplayName = displayName;
                }
                else
                {
                    _db.ConnectedAccounts.Add(new ConnectedAccount
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId.Value,
                        Platform = Platform.Spotify,
                        AccessToken = tokenResponse.access_token,
                        RefreshToken = tokenResponse.refresh_token,
                        ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in),
                        PlatformUserId = platformUserId,
                        PlatformDisplayName = displayName
                    });
                }

                await _db.SaveChangesAsync();
            }
        }

        return Redirect("http://localhost:4200/settings?connected=spotify");
    }

    // --- YouTube ---
    [HttpGet("connect/youtube")]
    public IActionResult ConnectYouTube()
    {
        var clientId = Environment.GetEnvironmentVariable("YOUTUBE_CLIENT_ID") ?? "";
        if (string.IsNullOrEmpty(clientId))
            return Ok(new { data = (object?)null, error = "YouTube client ID not configured" });

        var scopes = "https://www.googleapis.com/auth/youtube.readonly https://www.googleapis.com/auth/youtube";
        var jwt = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

        var url = $"https://accounts.google.com/o/oauth2/v2/auth?client_id={clientId}" +
                  $"&response_type=code&redirect_uri={Uri.EscapeDataString(YouTubeClient.RedirectUri)}" +
                  $"&scope={Uri.EscapeDataString(scopes)}" +
                  $"&access_type=offline&prompt=consent" +
                  $"&state={Uri.EscapeDataString(jwt)}";

        return Ok(new { data = new { url }, error = (string?)null });
    }

    [HttpGet("callback/youtube")]
    [AllowAnonymous]
    public async Task<IActionResult> CallbackYouTube([FromQuery] string code, [FromQuery] string? state)
    {
        var tokenResponse = await _youTubeClient.ObtainAccessToken(code);

        if (tokenResponse?.access_token == null)
            return Redirect("http://localhost:4200/settings?error=youtube_auth_failed");

        if (!string.IsNullOrEmpty(state))
        {
            var userId = ExtractUserIdFromJwt(state);
            if (userId != null)
            {
                var (platformUserId, displayName) = await _youTubeClient.GetCurrentUser(tokenResponse.access_token);

                var existing = await _db.ConnectedAccounts
                    .FirstOrDefaultAsync(ca => ca.UserId == userId.Value && ca.Platform == Platform.YouTube);

                if (existing != null)
                {
                    existing.AccessToken = tokenResponse.access_token;
                    existing.RefreshToken = tokenResponse.refresh_token ?? existing.RefreshToken;
                    existing.ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in);
                    existing.PlatformUserId = platformUserId;
                    existing.PlatformDisplayName = displayName;
                }
                else
                {
                    _db.ConnectedAccounts.Add(new ConnectedAccount
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId.Value,
                        Platform = Platform.YouTube,
                        AccessToken = tokenResponse.access_token,
                        RefreshToken = tokenResponse.refresh_token,
                        ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in),
                        PlatformUserId = platformUserId,
                        PlatformDisplayName = displayName
                    });
                }

                await _db.SaveChangesAsync();
            }
        }

        return Redirect("http://localhost:4200/settings?connected=youtube");
    }

    // --- Apple Music ---
    [HttpGet("connect/applemusic")]
    public IActionResult ConnectAppleMusic()
    {
        var developerToken = _appleMusicClient.DeveloperToken;
        if (string.IsNullOrEmpty(developerToken))
            return Ok(new { data = (object?)null, error = "Apple Music developer token not configured" });

        return Ok(new { data = new { developerToken }, error = (string?)null });
    }

    [HttpPost("connect/applemusic")]
    public async Task<IActionResult> SaveAppleMusicToken([FromBody] AppleMusicTokenRequest request)
    {
        var userId = GetUserId();

        var (platformUserId, displayName) = await _appleMusicClient.GetCurrentUser(request.UserToken);

        var existing = await _db.ConnectedAccounts
            .FirstOrDefaultAsync(ca => ca.UserId == userId && ca.Platform == Platform.AppleMusic);

        if (existing != null)
        {
            existing.AccessToken = request.UserToken;
            existing.PlatformUserId = platformUserId;
            existing.PlatformDisplayName = displayName;
        }
        else
        {
            _db.ConnectedAccounts.Add(new ConnectedAccount
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Platform = Platform.AppleMusic,
                AccessToken = request.UserToken,
                PlatformUserId = platformUserId,
                PlatformDisplayName = displayName
            });
        }

        await _db.SaveChangesAsync();
        return Ok(new { data = true, error = (string?)null });
    }

    // --- Stubs ---
    [HttpGet("connect/soundcloud")]
    public IActionResult ConnectSoundCloud()
    {
        return Ok(new { data = (object?)null, error = "SoundCloud integration coming soon" });
    }

    [HttpGet("connect/deezer")]
    public IActionResult ConnectDeezer()
    {
        return Ok(new { data = (object?)null, error = "Deezer integration coming soon" });
    }

    private Guid? ExtractUserIdFromJwt(string token)
    {
        try
        {
            var jwtSettings = _config.GetSection("Jwt");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!));

            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings["Issuer"],
                ValidAudience = jwtSettings["Audience"],
                IssuerSigningKey = key
            }, out _);

            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return userIdClaim != null ? Guid.Parse(userIdClaim) : null;
        }
        catch
        {
            return null;
        }
    }
}

[ApiController]
[Route("api/config")]
public class ConfigController : ControllerBase
{
    private readonly AppleMusicClient _appleMusicClient;

    public ConfigController(AppleMusicClient appleMusicClient)
    {
        _appleMusicClient = appleMusicClient;
    }

    [HttpGet("apple")]
    public IActionResult GetAppleConfig()
    {
        return Ok(new { data = new { developerToken = _appleMusicClient.DeveloperToken }, error = (string?)null });
    }
}

public class AppleMusicTokenRequest
{
    public string UserToken { get; set; } = string.Empty;
}
