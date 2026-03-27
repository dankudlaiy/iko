using System.Security.Claims;
using iko_host.Clients;
using iko_host.Data;
using iko_host.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace iko_host.Controllers;

[ApiController]
[Route("api/library")]
[Authorize]
public class LibraryController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly SpotifyClient _spotifyClient;
    private readonly YouTubeClient _youTubeClient;
    private readonly AppleMusicClient _appleMusicClient;

    public LibraryController(
        AppDbContext db,
        SpotifyClient spotifyClient,
        YouTubeClient youTubeClient,
        AppleMusicClient appleMusicClient)
    {
        _db = db;
        _spotifyClient = spotifyClient;
        _youTubeClient = youTubeClient;
        _appleMusicClient = appleMusicClient;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("playlists/{platform}")]
    public async Task<IActionResult> GetPlaylists(Platform platform)
    {
        var account = await GetAccount(platform);

        switch (platform)
        {
            case Platform.Spotify:
                if (account == null)
                    return BadRequest(new { data = (object?)null, error = "Spotify not connected" });
                return Ok(new { data = await GetSpotifyPlaylists(account.AccessToken), error = (string?)null });

            case Platform.YouTube:
                if (account == null)
                    return BadRequest(new { data = (object?)null, error = "YouTube not connected" });
                return Ok(new { data = await _youTubeClient.GetPlaylists(account.AccessToken), error = (string?)null });

            case Platform.AppleMusic:
                if (account == null)
                    return BadRequest(new { data = (object?)null, error = "Apple Music not connected" });
                return Ok(new { data = await _appleMusicClient.GetPlaylists(account.AccessToken), error = (string?)null });

            case Platform.SoundCloud:
            case Platform.Deezer:
                return Ok(new { data = Array.Empty<object>(), error = "Platform coming soon" });

            default:
                return Ok(new { data = Array.Empty<object>(), error = "Platform not supported" });
        }
    }

    [HttpGet("playlists/{platform}/{playlistId}/tracks")]
    public async Task<IActionResult> GetPlaylistTracks(Platform platform, string playlistId)
    {
        var account = await GetAccount(platform);

        switch (platform)
        {
            case Platform.Spotify:
                if (account == null)
                    return BadRequest(new { data = (object?)null, error = "Spotify not connected" });
                return Ok(new { data = await GetSpotifyPlaylistTracks(playlistId, account.AccessToken), error = (string?)null });

            case Platform.YouTube:
                if (account == null)
                    return BadRequest(new { data = (object?)null, error = "YouTube not connected" });
                return Ok(new { data = await _youTubeClient.GetPlaylistTracks(playlistId, account.AccessToken), error = (string?)null });

            case Platform.AppleMusic:
                if (account == null)
                    return BadRequest(new { data = (object?)null, error = "Apple Music not connected" });
                return Ok(new { data = await _appleMusicClient.GetPlaylistTracks(playlistId, account.AccessToken), error = (string?)null });

            default:
                return Ok(new { data = Array.Empty<object>(), error = "Platform not supported" });
        }
    }

    private async Task<ConnectedAccount?> GetAccount(Platform platform)
    {
        var userId = GetUserId();
        return await _db.ConnectedAccounts.FirstOrDefaultAsync(
            ca => ca.UserId == userId && ca.Platform == platform);
    }

    private async Task<List<object>> GetSpotifyPlaylists(string accessToken)
    {
        var httpClient = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.spotify.com/v1/me/playlists?limit=50");
        request.Headers.Add("Authorization", $"Bearer {accessToken}");

        var response = await httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        dynamic? obj = Newtonsoft.Json.JsonConvert.DeserializeObject(content);

        var playlists = new List<object>();
        if (obj?.items == null) return playlists;

        foreach (var item in obj.items)
        {
            string? imageUrl = null;
            if (item.images != null && item.images.HasValues)
                imageUrl = item.images[0].url.ToString();

            playlists.Add(new
            {
                id = item.id.ToString(),
                name = item.name.ToString(),
                imageUrl,
                trackCount = (int)(item.tracks?.total ?? 0)
            });
        }

        return playlists;
    }

    private async Task<List<object>> GetSpotifyPlaylistTracks(string playlistId, string accessToken)
    {
        var httpClient = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.spotify.com/v1/playlists/{playlistId}/tracks?limit=100");
        request.Headers.Add("Authorization", $"Bearer {accessToken}");

        var response = await httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        dynamic? obj = Newtonsoft.Json.JsonConvert.DeserializeObject(content);

        var tracks = new List<object>();
        if (obj?.items == null) return tracks;

        foreach (var item in obj.items)
        {
            var track = item.track;
            if (track == null) continue;

            string? imageUrl = null;
            if (track.album?.images != null && track.album.images.HasValues)
                imageUrl = track.album.images[0].url.ToString();

            var artists = new List<string>();
            foreach (var artist in track.artists)
                artists.Add(artist.name.ToString());

            tracks.Add(new
            {
                platformTrackId = track.id.ToString(),
                name = track.name.ToString(),
                artist = string.Join(", ", artists),
                imageUrl,
                durationMs = (int)(track.duration_ms ?? 0),
                platform = "Spotify"
            });
        }

        return tracks;
    }
}
