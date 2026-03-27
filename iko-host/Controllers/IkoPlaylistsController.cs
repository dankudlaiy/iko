using System.Security.Claims;
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

    public IkoPlaylistsController(AppDbContext db)
    {
        _db = db;
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
