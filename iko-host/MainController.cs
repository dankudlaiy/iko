using iko_host.Clients;
using iko_host.Models;
using Microsoft.AspNetCore.Mvc;

namespace iko_host;

public class MainController : ControllerBase
{
    [HttpPost("parse")]
    public async Task<IActionResult> ParseVkPlaylist([FromBody] ParsePlaylistRequest request)
    {
        if (string.IsNullOrEmpty(request.Link))
        {
            return BadRequest("playlist link cannot be empty");
        }

        var vkClient = new VkClient();
        var parsedTracks = await vkClient.ParseVkPlaylist(request.Link);

        return Ok(new { message = "vk to spotify", link = request.Link, tracks = parsedTracks });
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchForSpotifyTrack([FromQuery] string name, [FromQuery] string artist)
    {
        var spotifyClient = new SpotifyClient();

        var track = await spotifyClient.SearchForTrack(name, artist);

        return Ok(track);
    }

    [HttpGet("auth")]
    public async Task<IActionResult> Auth([FromQuery] string token)
    {
        var spotifyClient = new SpotifyClient();

        var bearer = await spotifyClient.ObtainAccessToken(token);

        return Ok(bearer);
    }

    [HttpPost("create")]
    public async Task<IActionResult> CreatePlaylist([FromBody] CreatePlaylistRequest request)
    {
        var spotifyClient = new SpotifyClient();

        var (playlistUrl, playlistImg) = await spotifyClient.CreatePlaylist(request.Ids, request.Token);

        return Ok(new { url = playlistUrl, img = playlistImg });
    }
}