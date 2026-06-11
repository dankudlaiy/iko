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
    private readonly PlatformClientFactory _clients;

    public LibraryController(AppDbContext db, PlatformClientFactory clients)
    {
        _db = db;
        _clients = clients;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("playlists/{platform}")]
    public async Task<IActionResult> GetPlaylists(Platform platform)
    {
        var account = await GetAccount(platform);
        if (account == null)
            return BadRequest(new { data = (object?)null, error = $"{platform} not connected" });

        var playlists = await _clients.Get(platform).GetPlaylists(account.AccessToken);
        return Ok(new { data = playlists, error = (string?)null });
    }

    [HttpGet("playlists/{platform}/{playlistId}/tracks")]
    public async Task<IActionResult> GetPlaylistTracks(Platform platform, string playlistId)
    {
        var account = await GetAccount(platform);
        if (account == null)
            return BadRequest(new { data = (object?)null, error = $"{platform} not connected" });

        var tracks = await _clients.Get(platform).GetPlaylistTracks(playlistId, account.AccessToken);
        return Ok(new { data = tracks, error = (string?)null });
    }

    private async Task<ConnectedAccount?> GetAccount(Platform platform)
    {
        var userId = GetUserId();
        return await _db.ConnectedAccounts.FirstOrDefaultAsync(
            ca => ca.UserId == userId && ca.Platform == platform);
    }
}
