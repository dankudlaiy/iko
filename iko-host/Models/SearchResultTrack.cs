namespace iko_host.Models;

public class SearchResultTrack
{
    public string PlatformTrackId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string? Album { get; set; }
    public string? ImageUrl { get; set; }
    public int DurationMs { get; set; }
    public bool Explicit { get; set; }
    public string Platform { get; set; } = string.Empty;
}
