namespace iko_host.Models;

public class ParseRequest
{
    public Platform Platform { get; set; }
    public string Link { get; set; } = string.Empty;
}

public class SearchRequest
{
    public List<TrackSearchItem> Tracks { get; set; } = new();
    public Platform TargetPlatform { get; set; }
}

public class TrackSearchItem
{
    public string Name { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
}

public class CreatePlaylistApiRequest
{
    public Platform TargetPlatform { get; set; }
    public List<string> TrackIds { get; set; } = new();
    public string PlaylistName { get; set; } = string.Empty;
    public string? AccessToken { get; set; }
}
