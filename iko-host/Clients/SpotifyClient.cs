namespace iko_host.Clients;

using System.Net.Http.Headers;
using System.Text;
using iko_host.Exceptions;
using Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class SpotifyClient : IPlatformClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SpotifyClient> _logger;

    private readonly string _clientId;
    private readonly string _clientSecret;

    public SpotifyClient(HttpClient httpClient, ILogger<SpotifyClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _clientId = Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_ID") ??
                    throw new InvalidOperationException("SPOTIFY_CLIENT_ID not found in environment");
        _clientSecret = Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_SECRET") ??
                        throw new InvalidOperationException("SPOTIFY_CLIENT_SECRET not found in environment");
    }

    public Platform Platform => Platform.Spotify;

    public async Task<TrackModel?> SearchForTrack(string name, string artist, string? accessToken = null)
    {
        accessToken ??= await GetAccessToken();

        var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.spotify.com/v1/search?type=track&limit=1&q={Uri.EscapeDataString($"{name} {artist}")}");

        request.Headers.Add("Authorization", $"Bearer {accessToken}");

        var trackResponse = await _httpClient.SendAsync(request);
        var responseContent = await trackResponse.Content.ReadAsStringAsync();

        if (!trackResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning("Spotify search failed for {Name} - {Artist}: HTTP {Status}",
                name, artist, (int)trackResponse.StatusCode);
            return null;
        }

        dynamic? obj = JsonConvert.DeserializeObject(responseContent);

        if (obj?.tracks?.items == null || !obj.tracks.items.HasValues)
            return null;

        string trackId = obj.tracks.items[0].id;
        string? imageUrl = obj.tracks.items[0].album?.images?[0]?.url?.ToString();
        int durationMs = (int)(obj.tracks.items[0].duration_ms ?? 0);

        return new TrackModel
        {
            Name = name,
            Artist = artist,
            Platform = Platform.Spotify,
            PlatformTrackId = trackId,
            ImageUrl = imageUrl,
            DurationMs = durationMs
        };
    }

    public async Task<List<PlaylistSummary>> GetPlaylists(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.spotify.com/v1/me/playlists?limit=50");
        request.Headers.Add("Authorization", $"Bearer {accessToken}");

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new PlatformApiException(Platform.Spotify,
                "Failed to load Spotify playlists", (int)response.StatusCode);

        dynamic? obj = JsonConvert.DeserializeObject(content);
        var playlists = new List<PlaylistSummary>();
        if (obj?.items == null) return playlists;

        foreach (var item in obj.items)
        {
            string? imageUrl = null;
            if (item.images != null && item.images.HasValues)
                imageUrl = item.images[0].url.ToString();

            playlists.Add(new PlaylistSummary
            {
                Id = item.id.ToString(),
                Name = item.name.ToString(),
                ImageUrl = imageUrl,
                TrackCount = (int)(item.tracks?.total ?? 0)
            });
        }

        return playlists;
    }

    public async Task<List<LibraryTrack>> GetPlaylistTracks(string playlistId, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.spotify.com/v1/playlists/{playlistId}/tracks?limit=100");
        request.Headers.Add("Authorization", $"Bearer {accessToken}");

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new PlatformApiException(Platform.Spotify,
                "Failed to load Spotify playlist tracks", (int)response.StatusCode);

        dynamic? obj = JsonConvert.DeserializeObject(content);
        var tracks = new List<LibraryTrack>();
        if (obj?.items == null) return tracks;

        foreach (var item in obj.items)
        {
            var track = item.track;
            if (track == null) continue;

            string? imageUrl = null;
            if (track.album?.images != null && track.album.images.HasValues)
                imageUrl = track.album.images[0].url.ToString();

            var artists = new List<string>();
            foreach (var artist in track.artists)
                artists.Add(artist.name.ToString());

            tracks.Add(new LibraryTrack
            {
                PlatformTrackId = track.id.ToString(),
                Name = track.name.ToString(),
                Artist = string.Join(", ", artists),
                ImageUrl = imageUrl,
                DurationMs = (int)(track.duration_ms ?? 0),
                Platform = "Spotify"
            });
        }

        return tracks;
    }

    public async Task<(string Url, string? ImageUrl)> CreatePlaylist(IEnumerable<string> trackIds, string accessToken, string? name = null)
    {
        var (userId, _) = await GetCurrentUser(accessToken);

        var createPlaylistData = new
        {
            name = name ?? $"iko — {DateTime.UtcNow:u}",
            description = "Created with iko"
        };

        var createRequest = new HttpRequestMessage(HttpMethod.Post, $"https://api.spotify.com/v1/users/{userId}/playlists");
        createRequest.Headers.Add("Authorization", $"Bearer {accessToken}");
        createRequest.Content = new StringContent(
            JsonConvert.SerializeObject(createPlaylistData),
            Encoding.UTF8,
            "application/json");

        var createResponse = await _httpClient.SendAsync(createRequest);
        if (!createResponse.IsSuccessStatusCode)
            throw new PlatformApiException(Platform.Spotify,
                "Failed to create Spotify playlist", (int)createResponse.StatusCode);

        var createResponseData = JsonConvert.DeserializeObject<dynamic>(await createResponse.Content.ReadAsStringAsync());

        var playlistId = createResponseData!.id.Value;
        var playlistUrl = (string)createResponseData.external_urls.spotify.Value;

        var addTracksUri = new Uri($"https://api.spotify.com/v1/playlists/{playlistId}/tracks");
        var trackUris = trackIds.Select(t => "spotify:track:" + t).ToList();

        const int chunkSize = 100;
        for (var i = 0; i < trackUris.Count; i += chunkSize)
        {
            var addTracksRequest = new HttpRequestMessage(HttpMethod.Post, addTracksUri);
            addTracksRequest.Headers.Add("Authorization", $"Bearer {accessToken}");
            addTracksRequest.Content = new StringContent(
                JsonConvert.SerializeObject(new { uris = trackUris.Skip(i).Take(chunkSize).ToList() }),
                Encoding.UTF8,
                "application/json");

            await _httpClient.SendAsync(addTracksRequest);
            await Task.Delay(2000);
        }

        await Task.Delay(2000);

        try
        {
            var getRequest = new HttpRequestMessage(HttpMethod.Get, $"https://api.spotify.com/v1/playlists/{playlistId}");
            getRequest.Headers.Add("Authorization", $"Bearer {accessToken}");

            var getResponse = await _httpClient.SendAsync(getRequest);
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var getResponseData = JsonConvert.DeserializeObject<dynamic>(getContent);

            var playlistImgUrl = (string?)getResponseData!.images[0].url.Value;
            return (playlistUrl, playlistImgUrl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch cover for created Spotify playlist {PlaylistId}", (string)playlistId);
            return (playlistUrl, null);
        }
    }

    public async Task<SpotifyTokenResponse?> ObtainAccessToken(string authToken, string redirectUri)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token");

        var base64Authorization = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_clientId}:{_clientSecret}"));
        request.Headers.TryAddWithoutValidation("Authorization", $"Basic {base64Authorization}");

        request.Content = new StringContent($"grant_type=authorization_code&code={authToken}&redirect_uri={Uri.EscapeDataString(redirectUri)}");
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");

        var response = await _httpClient.SendAsync(request);
        var jsonResponse = await response.Content.ReadAsStringAsync();

        return JsonConvert.DeserializeObject<SpotifyTokenResponse>(jsonResponse);
    }

    public async Task<SpotifyTokenResponse?> RefreshAccessToken(string refreshToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token");

        var base64Authorization = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_clientId}:{_clientSecret}"));
        request.Headers.TryAddWithoutValidation("Authorization", $"Basic {base64Authorization}");

        request.Content = new StringContent($"grant_type=refresh_token&refresh_token={refreshToken}");
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");

        var response = await _httpClient.SendAsync(request);
        var jsonResponse = await response.Content.ReadAsStringAsync();

        return JsonConvert.DeserializeObject<SpotifyTokenResponse>(jsonResponse);
    }

    public async Task<(string Id, string DisplayName)> GetCurrentUser(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.spotify.com/v1/me");
        request.Headers.Add("Authorization", $"Bearer {accessToken}");

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        dynamic obj = JsonConvert.DeserializeObject(content)!;
        return (obj.id.ToString(), obj.display_name?.ToString() ?? "");
    }

    private async Task<string?> GetAccessToken()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token");

        request.Headers.Add("Authorization", $"Basic {Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"))}");

        var bodyData = new List<KeyValuePair<string?, string?>>
        {
            new("grant_type", "client_credentials")
        };

        request.Content = new FormUrlEncodedContent(bodyData);

        var accessTokenResponse = await _httpClient.SendAsync(request);
        var accessToken = JObject.Parse(await accessTokenResponse.Content.ReadAsStringAsync())["access_token"]
            ?.ToString();

        return accessToken;
    }
}
