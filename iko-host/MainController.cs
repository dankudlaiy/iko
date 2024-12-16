using iko_host.Models;
using Microsoft.AspNetCore.Mvc;

namespace iko_host;

public class MainController : ControllerBase
{
    [HttpPost("transfer")]
    public IActionResult TransferPlaylist([FromBody] PlaylistRequest request)
    {
        if (string.IsNullOrEmpty(request.Link))
        {
            return BadRequest("Playlist link cannot be empty");
        }

        // Simulate playlist transfer logic here
        return Ok(new { message = "Playlist transfer successful", link = request.Link });
    }
}