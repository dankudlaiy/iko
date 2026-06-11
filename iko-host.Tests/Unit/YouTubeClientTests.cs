namespace iko_host.Tests.Unit;

using System.Net;
using iko_host.Clients;
using iko_host.Exceptions;
using iko_host.Models;
using iko_host.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;

public class YouTubeClientTests
{
    public YouTubeClientTests() => FakePlatformEnv.Set();

    private static YouTubeClient Client(StubHttpMessageHandler handler) =>
        new(new HttpClient(handler), NullLogger<YouTubeClient>.Instance);

    private static StubHttpMessageHandler SearchHandler() => new(req =>
        req.RequestUri!.AbsolutePath.Contains("/videos")
            ? StubHttpMessageHandler.Json(HttpStatusCode.OK,
                """{"items":[{"id":"vid-1","contentDetails":{"duration":"PT3M20S"}}]}""")
            : StubHttpMessageHandler.Json(HttpStatusCode.OK,
                """{"items":[{"id":{"videoId":"vid-1"},"snippet":{"thumbnails":{"medium":{"url":"https://img/yt"}}}}]}"""));

    [Fact]
    public async Task SearchForTrack_parses_result_and_duration()
    {
        var track = await Client(SearchHandler()).SearchForTrack("Song", "Artist", "oauth-token");

        Assert.NotNull(track);
        Assert.Equal("vid-1", track!.PlatformTrackId);
        Assert.Equal(Platform.YouTube, track.Platform);
        Assert.Equal(200000, track.DurationMs); // PT3M20S
    }

    [Fact]
    public async Task SearchForTrack_falls_back_to_api_key_without_token()
    {
        var handler = SearchHandler();

        var track = await Client(handler).SearchForTrack("Song", "Artist", accessToken: null);

        Assert.NotNull(track);
        Assert.Contains("key=test-youtube-key", handler.Requests[0].RequestUri!.Query);
    }

    [Fact]
    public async Task SearchForTrack_returns_null_without_token_and_api_key()
    {
        Environment.SetEnvironmentVariable("YOUTUBE_API_KEY", null);
        try
        {
            var handler = SearchHandler();
            Assert.Null(await Client(handler).SearchForTrack("Song", "Artist", accessToken: null));
            Assert.Empty(handler.Requests);
        }
        finally
        {
            FakePlatformEnv.Set();
        }
    }

    [Fact]
    public async Task SearchForTrack_survives_duration_endpoint_failure()
    {
        var handler = new StubHttpMessageHandler(req =>
            req.RequestUri!.AbsolutePath.Contains("/videos")
                ? StubHttpMessageHandler.Json(HttpStatusCode.InternalServerError, "boom")
                : StubHttpMessageHandler.Json(HttpStatusCode.OK,
                    """{"items":[{"id":{"videoId":"vid-1"},"snippet":{"thumbnails":{"medium":{"url":"https://img/yt"}}}}]}"""));

        var track = await Client(handler).SearchForTrack("Song", "Artist", "oauth-token");

        Assert.NotNull(track);
        Assert.Equal(0, track!.DurationMs);
    }

    [Fact]
    public async Task GetPlaylists_throws_PlatformApiException_on_http_error()
    {
        var handler = StubHttpMessageHandler.RespondingWith(HttpStatusCode.Forbidden, "{}");

        var ex = await Assert.ThrowsAsync<PlatformApiException>(() => Client(handler).GetPlaylists("expired"));
        Assert.Equal(Platform.YouTube, ex.Platform);
        Assert.Equal(403, ex.StatusCode);
    }
}
