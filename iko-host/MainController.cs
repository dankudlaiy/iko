using iko_host.Clients;
using iko_host.Models;
using Microsoft.AspNetCore.Mvc;

namespace iko_host;

public class MainController : ControllerBase
{
    [HttpPost("parse")]
    public async Task<IActionResult> ParseVkPlaylist([FromBody] PlaylistRequest request)
    {
        if (string.IsNullOrEmpty(request.Link))
        {
            return BadRequest("Playlist link cannot be empty");
        }

        var vkClient = new VkClient();
        var parsedTracks = await vkClient.ParseVkPlaylist(request.Link);
        
        return Ok(new { message = "Playlist parsing successful", link = request.Link, tracks = parsedTracks });
    }
}