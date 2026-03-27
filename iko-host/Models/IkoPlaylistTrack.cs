namespace iko_host.Models;

public class IkoPlaylistTrack
{
    public Guid Id { get; set; }
    public Guid PlaylistId { get; set; }
    public Platform Platform { get; set; }
    public string PlatformTrackId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int DurationMs { get; set; }
    public int Order { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    public IkoPlaylist Playlist { get; set; } = null!;
}
