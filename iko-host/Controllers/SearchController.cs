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
    private readonly AppDbContext _db;
    private readonly PlatformClientFactory _clients;
    private readonly ILogger<SearchController> _logger;

    public SearchController(AppDbContext db, PlatformClientFactory clients, ILogger<SearchController> logger)
    {
        _db = db;
        _clients = clients;
        _logger = logger;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] string? platforms = null)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { data = (object?)null, error = "Query is required" });

        var requested = (platforms ?? "Spotify,YouTube,AppleMusic")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => Enum.TryParse<Platform>(p, out _))
            .Select(Enum.Parse<Platform>)
            .ToList();

        var userId = GetUserId();
        var accounts = await _db.ConnectedAccounts
            .Where(ca => ca.UserId == userId && requested.Contains(ca.Platform))
            .ToDictionaryAsync(ca => ca.Platform, ca => ca.AccessToken);

        var results = new Dictionary<string, List<object>>();

        var searches = requested.Select(async platform =>
        {
            var found = new List<object>();
            try
            {
                var token = accounts.GetValueOrDefault(platform);
                var track = await _clients.Get(platform).SearchForTrack(q, "", token);
                if (track?.PlatformTrackId != null)
                {
                    found.Add(new
                    {
                        platformTrackId = track.PlatformTrackId,
                        name = track.Name,
                        artist = track.Artist,
                        imageUrl = track.ImageUrl,
                        durationMs = track.DurationMs
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Search on {Platform} failed for query {Query}", platform, q);
            }
            lock (results) { results[platform.ToString()] = found; }
        });

        await Task.WhenAll(searches);

        return Ok(new { data = results, error = (string?)null });
    }
}
