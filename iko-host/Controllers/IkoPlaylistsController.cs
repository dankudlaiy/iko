using System.Security.Claims;
using iko_host.Clients;
using iko_host.Data;
using iko_host.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace iko_host.Controllers;

[ApiController]
[Route("api/iko-playlists")]
[Authorize]
public class IkoPlaylistsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly PlatformClientFactory _clients;
    private readonly ILogger<IkoPlaylistsController> _logger;

    private static readonly Dictionary<string, string> AllowedImageTypes = new()
    {
        ["image/png"] = ".png",
        ["image/jpeg"] = ".jpg",
        ["image/webp"] = ".webp",
    };
    private const long MaxCoverBytes = 5 * 1024 * 1024;

    public IkoPlaylistsController(
        AppDbContext db,
        IWebHostEnvironment env,
        PlatformClientFactory clients,
        ILogger<IkoPlaylistsController> logger)
    {
        _db = db;
        _env = env;
        _clients = clients;
        _logger = logger;
    }

    private string CoversDir => Path.Combine(
        _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), "uploads", "covers");

    private void DeleteExistingCovers(Guid playlistId)
    {
        if (!Directory.Exists(CoversDir)) return;
        foreach (var existing in Directory.EnumerateFiles(CoversDir, $"{playlistId}-*"))
        {
            try { System.IO.File.Delete(existing); } catch { /* best effort */ }
        }
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var userId = GetUserId();
        var playlists = await _db.IkoPlaylists
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.UpdatedAt)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.CoverUrl,
                TrackCount = p.Tracks.Count,
                CoverImages = p.Tracks
                    .Where(t => t.ImageUrl != null)
                    .OrderBy(t => t.Order)
                    .Select(t => t.ImageUrl)
                    .Take(4)
                    .ToList(),
                p.CreatedAt,
                p.UpdatedAt
            })
            .ToListAsync();

        return Ok(new { data = playlists, error = (string?)null });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateIkoPlaylistRequest request)
    {
        var playlist = new IkoPlaylist
        {
            Id = Guid.NewGuid(),
            UserId = GetUserId(),
            Name = request.Name
        };

        _db.IkoPlaylists.Add(playlist);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            data = new { playlist.Id, playlist.Name, playlist.CoverUrl, TrackCount = 0, playlist.CreatedAt, playlist.UpdatedAt },
            error = (string?)null
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var userId = GetUserId();
        var playlist = await _db.IkoPlaylists
            .Include(p => p.Tracks.OrderBy(t => t.Order))
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);

        if (playlist == null)
            return NotFound(new { data = (object?)null, error = "Playlist not found" });

        return Ok(new
        {
            data = new
            {
                playlist.Id,
                playlist.Name,
                playlist.CoverUrl,
                playlist.CreatedAt,
                playlist.UpdatedAt,
                Tracks = playlist.Tracks.Select(t => new
                {
                    t.Id,
                    t.Platform,
                    t.PlatformTrackId,
                    t.Name,
                    t.Artist,
                    t.ImageUrl,
                    t.DurationMs,
                    t.Order,
                    t.AddedAt
                })
            },
            error = (string?)null
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateIkoPlaylistRequest request)
    {
        var userId = GetUserId();
        var playlist = await _db.IkoPlaylists.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);

        if (playlist == null)
            return NotFound(new { data = (object?)null, error = "Playlist not found" });

        playlist.Name = request.Name;
        playlist.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { data = new { playlist.Id, playlist.Name }, error = (string?)null });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetUserId();
        var playlist = await _db.IkoPlaylists.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);

        if (playlist == null)
            return NotFound(new { data = (object?)null, error = "Playlist not found" });

        _db.IkoPlaylists.Remove(playlist);
        await _db.SaveChangesAsync();

        return Ok(new { data = true, error = (string?)null });
    }

    [HttpPost("{id:guid}/tracks")]
    public async Task<IActionResult> AddTrack(Guid id, [FromBody] AddTrackRequest request)
    {
        var userId = GetUserId();
        var playlist = await _db.IkoPlaylists
            .Include(p => p.Tracks)
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);

        if (playlist == null)
            return NotFound(new { data = (object?)null, error = "Playlist not found" });

        var exists = playlist.Tracks.Any(t =>
            t.Platform == request.Platform && t.PlatformTrackId == request.PlatformTrackId);
        if (exists)
            return Conflict(new { data = (object?)null, error = "Track already exists in playlist" });

        var maxOrder = playlist.Tracks.Any() ? playlist.Tracks.Max(t => t.Order) : -1;

        var track = new IkoPlaylistTrack
        {
            Id = Guid.NewGuid(),
            PlaylistId = id,
            Platform = request.Platform,
            PlatformTrackId = request.PlatformTrackId,
            Name = request.Name,
            Artist = request.Artist,
            ImageUrl = request.ImageUrl,
            DurationMs = request.DurationMs,
            Order = maxOrder + 1
        };

        _db.IkoPlaylistTracks.Add(track);
        playlist.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new
        {
            data = new
            {
                track.Id, track.Platform, track.PlatformTrackId,
                track.Name, track.Artist, track.ImageUrl,
                track.DurationMs, track.Order, track.AddedAt
            },
            error = (string?)null
        });
    }

    [HttpDelete("{id:guid}/tracks/{trackId:guid}")]
    public async Task<IActionResult> RemoveTrack(Guid id, Guid trackId)
    {
        var userId = GetUserId();
        var playlist = await _db.IkoPlaylists.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
        if (playlist == null)
            return NotFound(new { data = (object?)null, error = "Playlist not found" });

        var track = await _db.IkoPlaylistTracks.FirstOrDefaultAsync(t => t.Id == trackId && t.PlaylistId == id);
        if (track == null)
            return NotFound(new { data = (object?)null, error = "Track not found" });

        _db.IkoPlaylistTracks.Remove(track);
        playlist.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { data = true, error = (string?)null });
    }

    [HttpPatch("{id:guid}/tracks/reorder")]
    public async Task<IActionResult> Reorder(Guid id, [FromBody] ReorderRequest request)
    {
        var userId = GetUserId();
        var playlist = await _db.IkoPlaylists
            .Include(p => p.Tracks)
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);

        if (playlist == null)
            return NotFound(new { data = (object?)null, error = "Playlist not found" });

        for (var i = 0; i < request.OrderedIds.Count; i++)
        {
            var track = playlist.Tracks.FirstOrDefault(t => t.Id == request.OrderedIds[i]);
            if (track != null) track.Order = i;
        }

        playlist.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { data = true, error = (string?)null });
    }

    [HttpPost("{id:guid}/cover")]
    public async Task<IActionResult> UploadCover(Guid id, IFormFile? file)
    {
        var userId = GetUserId();
        var playlist = await _db.IkoPlaylists.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
        if (playlist == null)
            return NotFound(new { data = (object?)null, error = "Playlist not found" });

        if (file == null || file.Length == 0)
            return BadRequest(new { data = (object?)null, error = "No file uploaded" });
        if (file.Length > MaxCoverBytes)
            return BadRequest(new { data = (object?)null, error = "Image must be 5 MB or smaller" });
        if (!AllowedImageTypes.TryGetValue(file.ContentType, out var ext))
            return BadRequest(new { data = (object?)null, error = "Only PNG, JPEG or WebP images are allowed" });

        Directory.CreateDirectory(CoversDir);
        DeleteExistingCovers(id);

        var fileName = $"{id}-{DateTime.UtcNow.Ticks}{ext}";
        var fullPath = Path.Combine(CoversDir, fileName);
        await using (var stream = System.IO.File.Create(fullPath))
        {
            await file.CopyToAsync(stream);
        }

        playlist.CoverUrl = $"/uploads/covers/{fileName}";
        playlist.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { data = new { playlist.Id, playlist.Name, playlist.CoverUrl }, error = (string?)null });
    }

    [HttpPost("{id:guid}/export")]
    public async Task<IActionResult> Export(Guid id, [FromBody] ExportPlaylistRequest request)
    {
        var userId = GetUserId();
        var playlist = await _db.IkoPlaylists
            .Include(p => p.Tracks.OrderBy(t => t.Order))
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);

        if (playlist == null)
            return NotFound(new { data = (object?)null, error = "Playlist not found" });
        if (playlist.Tracks.Count == 0)
            return BadRequest(new { data = (object?)null, error = "Playlist is empty" });

        var account = await _db.ConnectedAccounts.FirstOrDefaultAsync(
            ca => ca.UserId == userId && ca.Platform == request.TargetPlatform);
        if (account == null)
            return BadRequest(new { data = (object?)null, error = $"{request.TargetPlatform} is not connected" });

        var client = _clients.Get(request.TargetPlatform);
        var matchedIds = new List<string>();
        var unmatched = new List<object>();

        foreach (var track in playlist.Tracks)
        {
            if (track.Platform == request.TargetPlatform)
            {
                matchedIds.Add(track.PlatformTrackId);
                continue;
            }

            var found = await client.SearchForTrack(track.Name, track.Artist, account.AccessToken);
            if (found?.PlatformTrackId != null)
                matchedIds.Add(found.PlatformTrackId);
            else
                unmatched.Add(new { track.Name, track.Artist });
        }

        if (matchedIds.Count == 0)
            return BadRequest(new { data = (object?)null, error = "No tracks could be matched on the target platform" });

        var (url, imageUrl) = await client.CreatePlaylist(matchedIds, account.AccessToken, playlist.Name);

        _logger.LogInformation(
            "Exported playlist {PlaylistId} to {Platform}: {Matched}/{Total} tracks",
            id, request.TargetPlatform, matchedIds.Count, playlist.Tracks.Count);

        return Ok(new
        {
            data = new
            {
                url,
                imageUrl,
                matchedCount = matchedIds.Count,
                totalCount = playlist.Tracks.Count,
                unmatchedTracks = unmatched
            },
            error = (string?)null
        });
    }

    [HttpDelete("{id:guid}/cover")]
    public async Task<IActionResult> RemoveCover(Guid id)
    {
        var userId = GetUserId();
        var playlist = await _db.IkoPlaylists.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
        if (playlist == null)
            return NotFound(new { data = (object?)null, error = "Playlist not found" });

        DeleteExistingCovers(id);
        playlist.CoverUrl = null;
        playlist.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { data = new { playlist.Id, playlist.Name, playlist.CoverUrl }, error = (string?)null });
    }
}

public class CreateIkoPlaylistRequest
{
    public string Name { get; set; } = string.Empty;
}

public class UpdateIkoPlaylistRequest
{
    public string Name { get; set; } = string.Empty;
}

public class AddTrackRequest
{
    public Platform Platform { get; set; }
    public string PlatformTrackId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int DurationMs { get; set; }
}

public class ReorderRequest
{
    public List<Guid> OrderedIds { get; set; } = new();
}

public class ExportPlaylistRequest
{
    public Platform TargetPlatform { get; set; }
}
