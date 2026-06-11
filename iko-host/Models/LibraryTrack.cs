namespace iko_host.Models;

public class LibraryTrack
{
    public string PlatformTrackId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int DurationMs { get; set; }
    public string Platform { get; set; } = string.Empty; // "Spotify" | "YouTube" | "AppleMusic"
}
