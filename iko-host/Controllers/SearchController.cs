using System.Security.Claims;
using iko_host.Clients;
using iko_host.Data;
using iko_host.Models;
using iko_host.Services;
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
    private readonly AccountTokenService _tokens;
    private readonly ILogger<SearchController> _logger;

    public SearchController(
        AppDbContext db, PlatformClientFactory clients, AccountTokenService tokens, ILogger<SearchController> logger)
    {
        _db = db;
        _clients = clients;
        _tokens = tokens;
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
            .ToListAsync();

        // Refresh tokens sequentially before the parallel search: DbContext is not thread-safe.
        var tokens = new Dictionary<Platform, string>();
        foreach (var account in accounts)
            tokens[account.Platform] = await _tokens.GetValidAccessTokenAsync(account);

        var results = new Dictionary<string, List<object>>();

        var searches = requested.Select(async platform =>
        {
            var found = new List<object>();
            try
            {
                var token = tokens.GetValueOrDefault(platform);
                var tracks = await _clients.Get(platform).SearchTracks(q, 15, token);
                found.AddRange(tracks.Select(t => (object)new
                {
                    platformTrackId = t.PlatformTrackId,
                    name = t.Name,
                    artist = t.Artist,
                    album = t.Album,
                    imageUrl = t.ImageUrl,
                    durationMs = t.DurationMs,
                    @explicit = t.Explicit
                }));
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
