namespace iko_host.Tests.Unit;

using System.Net;
using iko_host.Clients;
using iko_host.Exceptions;
using iko_host.Models;
using iko_host.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;

public class AppleMusicClientTests
{
    public AppleMusicClientTests() => FakePlatformEnv.Set();

    private static AppleMusicClient Client(StubHttpMessageHandler handler) =>
        new(new HttpClient(handler), NullLogger<AppleMusicClient>.Instance);

    [Fact]
    public async Task SearchForTrack_returns_null_without_user_token()
    {
        var handler = StubHttpMessageHandler.RespondingWith(HttpStatusCode.OK, "{}");

        Assert.Null(await Client(handler).SearchForTrack("Song", "Artist", accessToken: null));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SearchForTrack_parses_song_and_expands_artwork_template()
    {
        var handler = StubHttpMessageHandler.RespondingWith(HttpStatusCode.OK,
            """{"results":{"songs":{"data":[{"id":"song-1","attributes":{"durationInMillis":180000,"artwork":{"url":"https://img/{w}x{h}.jpg"}}}]}}}""");

        var track = await Client(handler).SearchForTrack("Song", "Artist", "user-token");

        Assert.NotNull(track);
        Assert.Equal("song-1", track!.PlatformTrackId);
        Assert.Equal(Platform.AppleMusic, track.Platform);
        Assert.Equal("https://img/300x300.jpg", track.ImageUrl);
    }

    [Fact]
    public async Task GetPlaylistTracks_throws_PlatformApiException_on_http_error()
    {
        var handler = StubHttpMessageHandler.RespondingWith(HttpStatusCode.Unauthorized, "{}");

        var ex = await Assert.ThrowsAsync<PlatformApiException>(
            () => Client(handler).GetPlaylistTracks("pl-1", "bad-token"));
        Assert.Equal(Platform.AppleMusic, ex.Platform);
    }
}
