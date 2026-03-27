namespace iko_host.Models;

public class TrackModel
{
    public string Name { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int DurationMs { get; set; }
    public string? SpotifyId { get; set; }
    public string? YouTubeVideoId { get; set; }
    public string? AppleMusicId { get; set; }
    public bool Matched { get; set; }
}