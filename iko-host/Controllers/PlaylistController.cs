using System.Security.Claims;
using iko_host.Clients;
using iko_host.Data;
using iko_host.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace iko_host.Controllers;

[ApiController]
[Route("api/playlist")]
[Authorize]
public class PlaylistController : ControllerBase
{
    private readonly SpotifyClient _spotifyClient;
    private readonly YouTubeClient _youTubeClient;
    private readonly AppleMusicClient _appleMusicClient;
    private readonly AppDbContext _db;

    public PlaylistController(
        SpotifyClient spotifyClient,
        YouTubeClient youTubeClient,
        AppleMusicClient appleMusicClient,
        AppDbContext db)
    {
        _spotifyClient = spotifyClient;
        _youTubeClient = youTubeClient;
        _appleMusicClient = appleMusicClient;
        _db = db;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("parse")]
    public async Task<IActionResult> Parse([FromBody] ParseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Link))
            return BadRequest(new { data = (object?)null, error = "Playlist link is required" });

        List<TrackModel> tracks;
        var account = await GetConnectedAccount(request.Platform);

        switch (request.Platform)
        {
            case Platform.Spotify:
                if (account == null)
                    return BadRequest(new { data = (object?)null, error = "Spotify account not connected" });
                tracks = await _spotifyClient.ParsePlaylist(
                    ExtractPlaylistId(request.Link), account.AccessToken);
                break;

            case Platform.YouTube:
                if (account == null)
                    return BadRequest(new { data = (object?)null, error = "YouTube account not connected" });
                tracks = await _youTubeClient.ParsePlaylist(
                    ExtractPlaylistId(request.Link), account.AccessToken);
                break;

            case Platform.AppleMusic:
                if (account == null)
                    return BadRequest(new { data = (object?)null, error = "Apple Music account not connected" });
                tracks = await _appleMusicClient.ParsePlaylist(
                    ExtractPlaylistId(request.Link), account.AccessToken);
                break;

            case Platform.SoundCloud:
            case Platform.Deezer:
                return Ok(new { data = (object?)null, error = "Platform not yet supported" });

            default:
                return Ok(new { data = (object?)null, error = "Platform not yet supported" });
        }

        return Ok(new { data = new { tracks }, error = (string?)null });
    }

    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] SearchRequest request)
    {
        var results = new List<TrackModel?>();

        switch (request.TargetPlatform)
        {
            case Platform.Spotify:
                foreach (var t in request.Tracks)
                {
                    var result = await _spotifyClient.SearchForTrack(t.Name, t.Artist);
                    results.Add(result);
                }
                break;

            case Platform.YouTube:
                var ytAccount = await GetConnectedAccount(Platform.YouTube);
                if (ytAccount == null)
                    return BadRequest(new { data = (object?)null, error = "YouTube account not connected" });
                foreach (var t in request.Tracks)
                {
                    var result = await _youTubeClient.SearchForTrack(t.Name, t.Artist, ytAccount.AccessToken);
                    results.Add(result);
                }
                break;

            case Platform.AppleMusic:
                var amAccount = await GetConnectedAccount(Platform.AppleMusic);
                if (amAccount == null)
                    return BadRequest(new { data = (object?)null, error = "Apple Music account not connected" });
                foreach (var t in request.Tracks)
                {
                    var result = await _appleMusicClient.SearchForTrack(t.Name, t.Artist, amAccount.AccessToken);
                    results.Add(result);
                }
                break;

            case Platform.SoundCloud:
            case Platform.Deezer:
                return Ok(new { data = (object?)null, error = "Platform not yet supported" });

            default:
                return Ok(new { data = (object?)null, error = "Platform not yet supported" });
        }

        var matched = results.Where(r => r != null).ToList();
        return Ok(new { data = new { tracks = matched }, error = (string?)null });
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create([FromBody] CreatePlaylistApiRequest request)
    {
        switch (request.TargetPlatform)
        {
            case Platform.Spotify:
                var spotifyToken = request.AccessToken;
                if (string.IsNullOrEmpty(spotifyToken))
                {
                    var account = await GetConnectedAccount(Platform.Spotify);
                    spotifyToken = account?.AccessToken;
                }
                if (string.IsNullOrEmpty(spotifyToken))
                    return BadRequest(new { data = (object?)null, error = "Spotify access token is required" });

                var (spotifyUrl, spotifyImg) = await _spotifyClient.CreatePlaylist(
                    request.TrackIds, spotifyToken, request.PlaylistName);
                return Ok(new { data = new { url = spotifyUrl, img = spotifyImg }, error = (string?)null });

            case Platform.YouTube:
                var ytToken = request.AccessToken;
                if (string.IsNullOrEmpty(ytToken))
                {
                    var account = await GetConnectedAccount(Platform.YouTube);
                    ytToken = account?.AccessToken;
                }
                if (string.IsNullOrEmpty(ytToken))
                    return BadRequest(new { data = (object?)null, error = "YouTube access token is required" });

                var (ytUrl, ytImg) = await _youTubeClient.CreatePlaylist(
                    request.TrackIds, ytToken, request.PlaylistName);
                return Ok(new { data = new { url = ytUrl, img = ytImg }, error = (string?)null });

            case Platform.AppleMusic:
                var amToken = request.AccessToken;
                if (string.IsNullOrEmpty(amToken))
                {
                    var account = await GetConnectedAccount(Platform.AppleMusic);
                    amToken = account?.AccessToken;
                }
                if (string.IsNullOrEmpty(amToken))
                    return BadRequest(new { data = (object?)null, error = "Apple Music access token is required" });

                var (amUrl, amImg) = await _appleMusicClient.CreatePlaylist(
                    request.TrackIds, amToken, request.PlaylistName);
                return Ok(new { data = new { url = amUrl, img = amImg }, error = (string?)null });

            case Platform.SoundCloud:
            case Platform.Deezer:
                return Ok(new { data = (object?)null, error = "Platform not yet supported" });

            default:
                return Ok(new { data = (object?)null, error = "Platform not yet supported" });
        }
    }

    private async Task<ConnectedAccount?> GetConnectedAccount(Platform platform)
    {
        var userId = GetUserId();
        return await _db.ConnectedAccounts
            .FirstOrDefaultAsync(ca => ca.UserId == userId && ca.Platform == platform);
    }

    private static string ExtractPlaylistId(string link)
    {
        var uri = new Uri(link);
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Last().Split('?')[0];
    }
}
