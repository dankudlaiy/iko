namespace iko_host.Tests.Integration;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

public class IkoPlaylistsApiTests : IClassFixture<IkoApiFactory>
{
    private readonly HttpClient _client;

    public IkoPlaylistsApiTests(IkoApiFactory factory) => _client = factory.CreateClient();

    private static async Task<JsonElement> Body(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private async Task AuthorizeAsNewUser()
    {
        var email = $"{Guid.NewGuid():N}@test.com";
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new { email, password = "password123" });
        response.EnsureSuccessStatusCode();
        var token = (await Body(response)).GetProperty("data").GetProperty("token").GetString();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<string> CreatePlaylist(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/iko-playlists", new { name });
        response.EnsureSuccessStatusCode();
        return (await Body(response)).GetProperty("data").GetProperty("id").GetString()!;
    }

    private static object Track(string id, string name = "Song") => new
    {
        platform = 0, platformTrackId = id, name, artist = "Artist",
        imageUrl = (string?)null, durationMs = 1000
    };

    [Fact]
    public async Task Endpoints_require_authentication()
    {
        var response = await _client.GetAsync("/api/iko-playlists");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_list_get_round_trip()
    {
        await AuthorizeAsNewUser();
        var id = await CreatePlaylist("Road Trip");

        var list = await Body(await _client.GetAsync("/api/iko-playlists"));
        Assert.Contains(list.GetProperty("data").EnumerateArray(),
            p => p.GetProperty("id").GetString() == id);

        var detail = await Body(await _client.GetAsync($"/api/iko-playlists/{id}"));
        Assert.Equal("Road Trip", detail.GetProperty("data").GetProperty("name").GetString());
    }

    [Fact]
    public async Task Duplicate_track_returns_409()
    {
        await AuthorizeAsNewUser();
        var id = await CreatePlaylist("Dups");

        var first = await _client.PostAsJsonAsync($"/api/iko-playlists/{id}/tracks", Track("t-1"));
        first.EnsureSuccessStatusCode();
        var second = await _client.PostAsJsonAsync($"/api/iko-playlists/{id}/tracks", Track("t-1"));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Reorder_persists_new_order()
    {
        await AuthorizeAsNewUser();
        var id = await CreatePlaylist("Ordered");
        var t1 = await Body(await _client.PostAsJsonAsync($"/api/iko-playlists/{id}/tracks", Track("t-1", "First")));
        var t2 = await Body(await _client.PostAsJsonAsync($"/api/iko-playlists/{id}/tracks", Track("t-2", "Second")));
        var id1 = t1.GetProperty("data").GetProperty("id").GetString();
        var id2 = t2.GetProperty("data").GetProperty("id").GetString();

        var reorder = await _client.PatchAsJsonAsync($"/api/iko-playlists/{id}/tracks/reorder",
            new { orderedIds = new[] { id2, id1 } });
        reorder.EnsureSuccessStatusCode();

        var detail = await Body(await _client.GetAsync($"/api/iko-playlists/{id}"));
        var names = detail.GetProperty("data").GetProperty("tracks").EnumerateArray()
            .Select(t => t.GetProperty("name").GetString()).ToList();
        Assert.Equal(new[] { "Second", "First" }, names);
    }

    [Fact]
    public async Task Delete_removes_playlist()
    {
        await AuthorizeAsNewUser();
        var id = await CreatePlaylist("Doomed");

        var delete = await _client.DeleteAsync($"/api/iko-playlists/{id}");
        delete.EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/iko-playlists/{id}")).StatusCode);
    }

    [Fact]
    public async Task Export_to_unconnected_platform_returns_400()
    {
        await AuthorizeAsNewUser();
        var id = await CreatePlaylist("Exportable");
        (await _client.PostAsJsonAsync($"/api/iko-playlists/{id}/tracks", Track("t-1"))).EnsureSuccessStatusCode();

        var response = await _client.PostAsJsonAsync($"/api/iko-playlists/{id}/export", new { targetPlatform = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("not connected", (await Body(response)).GetProperty("error").GetString());
    }
}
