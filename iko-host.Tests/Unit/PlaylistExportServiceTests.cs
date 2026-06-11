namespace iko_host.Tests.Unit;

using iko_host.Clients;
using iko_host.Models;
using iko_host.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

public class PlaylistExportServiceTests
{
    private readonly Mock<IPlatformClient> _spotify = new();

    private PlaylistExportService Service()
    {
        _spotify.SetupGet(c => c.Platform).Returns(Platform.Spotify);
        _spotify.Setup(c => c.CreatePlaylist(It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(("https://spotify/playlist/1", (string?)"https://img"));
        var factory = new PlatformClientFactory(new[] { _spotify.Object });
        return new PlaylistExportService(factory, NullLogger<PlaylistExportService>.Instance);
    }

    private static IkoPlaylistTrack Track(Platform platform, string id, string name = "Song", string artist = "Artist") =>
        new() { Platform = platform, PlatformTrackId = id, Name = name, Artist = artist };

    [Fact]
    public async Task Same_platform_tracks_are_used_directly_without_search()
    {
        var service = Service();
        var tracks = new[] { Track(Platform.Spotify, "sp-1"), Track(Platform.Spotify, "sp-2") };

        var outcome = await service.ExportAsync(tracks, "My List", Platform.Spotify, "token");

        Assert.NotNull(outcome);
        Assert.Equal(2, outcome!.MatchedCount);
        Assert.Empty(outcome.UnmatchedTracks);
        _spotify.Verify(c => c.SearchForTrack(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
        _spotify.Verify(c => c.CreatePlaylist(
            It.Is<IEnumerable<string>>(ids => ids.SequenceEqual(new[] { "sp-1", "sp-2" })),
            "token", "My List"), Times.Once);
    }

    [Fact]
    public async Task Cross_platform_tracks_are_searched_and_unmatched_collected()
    {
        var service = Service();
        _spotify.Setup(c => c.SearchForTrack("Found", "A", "token"))
            .ReturnsAsync(new TrackModel { Platform = Platform.Spotify, PlatformTrackId = "found-id" });
        _spotify.Setup(c => c.SearchForTrack("Missing", "B", "token"))
            .ReturnsAsync((TrackModel?)null);
        var tracks = new[]
        {
            Track(Platform.YouTube, "yt-1", "Found", "A"),
            Track(Platform.YouTube, "yt-2", "Missing", "B")
        };

        var outcome = await service.ExportAsync(tracks, "Mixed", Platform.Spotify, "token");

        Assert.NotNull(outcome);
        Assert.Equal(1, outcome!.MatchedCount);
        Assert.Equal(2, outcome.TotalCount);
        var unmatched = Assert.Single(outcome.UnmatchedTracks);
        Assert.Equal("Missing", unmatched.Name);
        Assert.Equal("B", unmatched.Artist);
        _spotify.Verify(c => c.CreatePlaylist(
            It.Is<IEnumerable<string>>(ids => ids.SequenceEqual(new[] { "found-id" })),
            "token", "Mixed"), Times.Once);
    }

    [Fact]
    public async Task Returns_null_and_creates_nothing_when_no_track_matches()
    {
        var service = Service();
        _spotify.Setup(c => c.SearchForTrack(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync((TrackModel?)null);
        var tracks = new[] { Track(Platform.YouTube, "yt-1") };

        var outcome = await service.ExportAsync(tracks, "Empty", Platform.Spotify, "token");

        Assert.Null(outcome);
        _spotify.Verify(c => c.CreatePlaylist(It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }
}
