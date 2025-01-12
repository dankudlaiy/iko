namespace iko_host.Models;

public class ParsePlaylistRequest
{
    public string Link { get; set; }
}

public class CreatePlaylistRequest
{
    public List<string> Ids { get; set; }

    public string Token { get; set; }
}