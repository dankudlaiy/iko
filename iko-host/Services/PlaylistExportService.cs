namespace iko_host.Services;

using iko_host.Clients;
using iko_host.Models;

public record UnmatchedTrack(string Name, string Artist);

public record ExportOutcome(
    string Url,
    string? ImageUrl,
    int MatchedCount,
    int TotalCount,
    List<UnmatchedTrack> UnmatchedTracks);

public class PlaylistExportService
{
    private readonly PlatformClientFactory _clients;
    private readonly ILogger<PlaylistExportService> _logger;

    public PlaylistExportService(PlatformClientFactory clients, ILogger<PlaylistExportService> logger)
    {
        _clients = clients;
        _logger = logger;
    }

    /// <summary>
    /// Matches tracks on the target platform and creates a playlist there.
    /// Returns null when no track could be matched (nothing is created).
    /// </summary>
    public async Task<ExportOutcome?> ExportAsync(
        IReadOnlyList<IkoPlaylistTrack> tracks,
        string playlistName,
        Platform targetPlatform,
        string accessToken)
    {
        var client = _clients.Get(targetPlatform);
        var matchedIds = new List<string>();
        var unmatched = new List<UnmatchedTrack>();

        foreach (var track in tracks)
        {
            if (track.Platform == targetPlatform)
            {
                matchedIds.Add(track.PlatformTrackId);
                continue;
            }

            var found = await client.SearchForTrack(track.Name, track.Artist, accessToken);
            if (found?.PlatformTrackId != null)
                matchedIds.Add(found.PlatformTrackId);
            else
                unmatched.Add(new UnmatchedTrack(track.Name, track.Artist));
        }

        if (matchedIds.Count == 0)
            return null;

        var (url, imageUrl) = await client.CreatePlaylist(matchedIds, accessToken, playlistName);

        _logger.LogInformation(
            "Exported playlist {Name} to {Platform}: {Matched}/{Total} tracks",
            playlistName, targetPlatform, matchedIds.Count, tracks.Count);

        return new ExportOutcome(url, imageUrl, matchedIds.Count, tracks.Count, unmatched);
    }
}
