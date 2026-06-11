namespace iko_host.Models;

public class PlaylistSummary
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int TrackCount { get; set; }
}
