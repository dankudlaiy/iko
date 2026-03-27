namespace iko_host.Clients;

using System.Net.Http.Headers;
using System.Text;
using Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// ReSharper disable once ClassNeverInstantiated.Global
public class SpotifyClient
{
    public const string RedirectUri = "http://127.0.0.1:5000/api/accounts/callback/spotify";

    private readonly HttpClient _httpClient = new();

    private readonly string _clientId;
    private readonly string _clientSecret;

    public SpotifyClient()
    {
        _clientId = Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_ID") ?? 
                    throw new InvalidOperationException("SPOTIFY_CLIENT_ID not found in environment");
        _clientSecret = Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_SECRET") ?? 
                        throw new InvalidOperationException("SPOTIFY_CLIENT_SECRET not found in environment");
    }

    public async Task<TrackModel?> SearchForTrack(string name, string artist, string? accessToken = null)
    {
        accessToken ??= await GetAccessToken();

        var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.spotify.com/v1/search?type=track&limit=1&q={Uri.EscapeDataString($"{name} {artist}")}");
        
        request.Headers.Add("Authorization", $"Bearer {accessToken}");

        var trackResponse = await _httpClient.SendAsync(request);
        var responseContent = await trackResponse.Content.ReadAsStringAsync();

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
            SpotifyId = trackId,
            ImageUrl = imageUrl,
            DurationMs = durationMs,
            Matched = true
        };
    }

    public async Task<List<TrackModel>> ParsePlaylist(string playlistId, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.spotify.com/v1/playlists/{playlistId}/tracks?limit=100");
        request.Headers.Add("Authorization", $"Bearer {accessToken}");

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        dynamic? obj = JsonConvert.DeserializeObject(content);
        var tracks = new List<TrackModel>();

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

            tracks.Add(new TrackModel
            {
                Name = track.name.ToString(),
                Artist = string.Join(", ", artists),
                SpotifyId = track.id.ToString(),
                ImageUrl = imageUrl ?? string.Empty,
                DurationMs = (int)track.duration_ms,
                Matched = true
            });
        }

        return tracks;
    }

    public async Task<(string, string?)> CreatePlaylist(IEnumerable<string> ids, string accessToken, string? name = null)
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
        var createResponseData = JsonConvert.DeserializeObject<dynamic>(await createResponse.Content.ReadAsStringAsync());

        var playlistId = createResponseData!.id.Value;
        var playlistUrl = (string)createResponseData.external_urls.spotify.Value;

        var addTracksUri = new Uri($"https://api.spotify.com/v1/playlists/{playlistId}/tracks");
        var trackUris = ids.Select(t => "spotify:track:" + t).ToList();

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
        catch
        {
            return (playlistUrl, null);
        }
    }

    public async Task<SpotifyTokenResponse?> ObtainAccessToken(string authToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token");
        
        var base64Authorization = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_clientId}:{_clientSecret}"));
        request.Headers.TryAddWithoutValidation("Authorization", $"Basic {base64Authorization}");

        request.Content = new StringContent($"grant_type=authorization_code&code={authToken}&redirect_uri={Uri.EscapeDataString(RedirectUri)}");
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