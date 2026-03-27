using System.Security.Claims;
using iko_host.Clients;
using iko_host.Data;
using iko_host.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace iko_host.Controllers;

[ApiController]
[Route("api/search")]
[Authorize]
public class SearchController : ControllerBase
{
    private readonly SpotifyClient _spotifyClient;
    private readonly YouTubeClient _youTubeClient;
    private readonly AppleMusicClient _appleMusicClient;
    private readonly AppDbContext _db;

    public SearchController(
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

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] string? platforms = null)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { data = (object?)null, error = "Query is required" });

        var requestedPlatforms = (platforms ?? "Spotify,YouTube,AppleMusic")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var userId = GetUserId();
        var results = new Dictionary<string, List<object>>();
        var tasks = new List<Task>();

        foreach (var platform in requestedPlatforms)
        {
            switch (platform)
            {
                case "Spotify":
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            var track = await _spotifyClient.SearchForTrack(q, "");
                            lock (results)
                            {
                                results["Spotify"] = new List<object>();
                                if (track != null)
                                {
                                    results["Spotify"].Add(new
                                    {
                                        platformTrackId = track.SpotifyId ?? "",
                                        name = track.Name,
                                        artist = track.Artist,
                                        imageUrl = track.ImageUrl,
                                        durationMs = track.DurationMs
                                    });
                                }
                            }
                        }
                        catch
                        {
                            lock (results) { results["Spotify"] = new List<object>(); }
                        }
                    }));
                    break;

                case "YouTube":
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            var account = await _db.ConnectedAccounts
                                .FirstOrDefaultAsync(ca => ca.UserId == userId && ca.Platform == Platform.YouTube);

                            lock (results) { results["YouTube"] = new List<object>(); }

                            // YouTube search works with API key (no connected account needed)
                            var track = await _youTubeClient.SearchForTrack(q, "", account?.AccessToken);
                            if (track != null)
                            {
                                lock (results)
                                {
                                    results["YouTube"].Add(new
                                    {
                                        platformTrackId = track.YouTubeVideoId ?? "",
                                        name = track.Name,
                                        artist = track.Artist,
                                        imageUrl = track.ImageUrl,
                                        durationMs = track.DurationMs
                                    });
                                }
                            }
                        }
                        catch
                        {
                            lock (results) { results["YouTube"] = new List<object>(); }
                        }
                    }));
                    break;

                case "AppleMusic":
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            var account = await _db.ConnectedAccounts
                                .FirstOrDefaultAsync(ca => ca.UserId == userId && ca.Platform == Platform.AppleMusic);

                            lock (results) { results["AppleMusic"] = new List<object>(); }

                            if (account == null) return;

                            var track = await _appleMusicClient.SearchForTrack(q, "", account.AccessToken);
                            if (track != null)
                            {
                                lock (results)
                                {
                                    results["AppleMusic"].Add(new
                                    {
                                        platformTrackId = track.AppleMusicId ?? "",
                                        name = track.Name,
                                        artist = track.Artist,
                                        imageUrl = track.ImageUrl,
                                        durationMs = track.DurationMs
                                    });
                                }
                            }
                        }
                        catch
                        {
                            lock (results) { results["AppleMusic"] = new List<object>(); }
                        }
                    }));
                    break;
            }
        }

        await Task.WhenAll(tasks);

        return Ok(new { data = results, error = (string?)null });
    }
}
