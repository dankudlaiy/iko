namespace iko_host.Tests.Unit;

using System.Net;
using iko_host.Clients;
using iko_host.Exceptions;
using iko_host.Models;
using iko_host.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;

public class SpotifyClientTests
{
    public SpotifyClientTests() => FakePlatformEnv.Set();

    private static SpotifyClient Client(StubHttpMessageHandler handler) =>
        new(new HttpClient(handler), NullLogger<SpotifyClient>.Instance);

    [Fact]
    public async Task SearchForTrack_parses_first_result()
    {
        var handler = StubHttpMessageHandler.RespondingWith(HttpStatusCode.OK,
            """{"tracks":{"items":[{"id":"track-1","duration_ms":215000,"album":{"images":[{"url":"https://img/cover"}]}}]}}""");

        var track = await Client(handler).SearchForTrack("Song", "Artist", "user-token");

        Assert.NotNull(track);
        Assert.Equal("track-1", track!.PlatformTrackId);
        Assert.Equal(Platform.Spotify, track.Platform);
        Assert.Equal(215000, track.DurationMs);
        Assert.Equal("https://img/cover", track.ImageUrl);
    }

    [Fact]
    public async Task SearchForTrack_returns_null_when_api_fails()
    {
        var handler = StubHttpMessageHandler.RespondingWith(HttpStatusCode.TooManyRequests, "{}");

        var track = await Client(handler).SearchForTrack("Song", "Artist", "user-token");

        Assert.Null(track);
    }

    [Fact]
    public async Task SearchForTrack_returns_null_when_no_items()
    {
        var handler = StubHttpMessageHandler.RespondingWith(HttpStatusCode.OK,
            """{"tracks":{"items":[]}}""");

        Assert.Null(await Client(handler).SearchForTrack("Song", "Artist", "user-token"));
    }

    [Fact]
    public async Task GetPlaylists_maps_summary_fields()
    {
        var handler = StubHttpMessageHandler.RespondingWith(HttpStatusCode.OK,
            """{"items":[{"id":"pl-1","name":"Mix","images":[{"url":"https://img/p"}],"tracks":{"total":12}}]}""");

        var playlists = await Client(handler).GetPlaylists("user-token");

        var pl = Assert.Single(playlists);
        Assert.Equal("pl-1", pl.Id);
        Assert.Equal("Mix", pl.Name);
        Assert.Equal("https://img/p", pl.ImageUrl);
        Assert.Equal(12, pl.TrackCount);
    }

    [Fact]
    public async Task GetPlaylists_throws_PlatformApiException_on_http_error()
    {
        var handler = StubHttpMessageHandler.RespondingWith(HttpStatusCode.Unauthorized, "{}");

        var ex = await Assert.ThrowsAsync<PlatformApiException>(() => Client(handler).GetPlaylists("expired"));
        Assert.Equal(Platform.Spotify, ex.Platform);
        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task SearchTracks_returns_multiple_with_album_and_explicit()
    {
        var handler = StubHttpMessageHandler.RespondingWith(HttpStatusCode.OK,
            """{"tracks":{"items":[{"id":"a","name":"Song A","duration_ms":200000,"explicit":true,"album":{"name":"Alb","images":[{"url":"https://img/a"}]},"artists":[{"name":"X"}]},{"id":"b","name":"Song B","duration_ms":180000,"explicit":false,"album":{"name":"Alb2","images":[]},"artists":[{"name":"Y"},{"name":"Z"}]}]}}""");

        var res = await Client(handler).SearchTracks("q", 15, "user-token");

        Assert.Equal(2, res.Count);
        Assert.Equal("a", res[0].PlatformTrackId);
        Assert.Equal("Alb", res[0].Album);
        Assert.True(res[0].Explicit);
        Assert.Equal("https://img/a", res[0].ImageUrl);
        Assert.Equal("Y, Z", res[1].Artist);
        Assert.Equal("Spotify", res[0].Platform);
    }

    [Fact]
    public async Task SearchTracks_returns_empty_on_http_error()
    {
        var handler = StubHttpMessageHandler.RespondingWith(HttpStatusCode.TooManyRequests, "{}");
        Assert.Empty(await Client(handler).SearchTracks("q", 15, "user-token"));
    }

    [Fact]
    public async Task GetPlaylistTracks_maps_library_tracks()
    {
        var handler = StubHttpMessageHandler.RespondingWith(HttpStatusCode.OK,
            """{"items":[{"track":{"id":"t-1","name":"Tune","duration_ms":1000,"album":{"images":[{"url":"https://img/t"}]},"artists":[{"name":"A"},{"name":"B"}]}}]}""");

        var tracks = await Client(handler).GetPlaylistTracks("pl-1", "user-token");

        var t = Assert.Single(tracks);
        Assert.Equal("t-1", t.PlatformTrackId);
        Assert.Equal("A, B", t.Artist);
        Assert.Equal("Spotify", t.Platform);
    }
}
