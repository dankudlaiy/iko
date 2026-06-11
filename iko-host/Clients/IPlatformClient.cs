namespace iko_host.Clients;

using iko_host.Models;

public interface IPlatformClient
{
    Platform Platform { get; }

    Task<List<PlaylistSummary>> GetPlaylists(string accessToken);
    Task<List<LibraryTrack>> GetPlaylistTracks(string playlistId, string accessToken);
    Task<TrackModel?> SearchForTrack(string name, string artist, string? accessToken = null);
    Task<(string Url, string? ImageUrl)> CreatePlaylist(IEnumerable<string> trackIds, string accessToken, string? name = null);
}
