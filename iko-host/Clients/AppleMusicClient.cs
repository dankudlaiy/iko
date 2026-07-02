namespace iko_host.Clients;

using iko_host.Exceptions;
using Models;
using Newtonsoft.Json;

public class AppleMusicClient : IPlatformClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AppleMusicClient> _logger;
    private readonly string _developerToken;

    public AppleMusicClient(HttpClient httpClient, ILogger<AppleMusicClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _developerToken = Environment.GetEnvironmentVariable("APPLE_DEVELOPER_TOKEN") ?? "";
    }

    public Platform Platform => Platform.AppleMusic;

    public string DeveloperToken => _developerToken;

    public async Task<TrackModel?> SearchForTrack(string name, string artist, string? accessToken = null)
    {
        if (string.IsNullOrEmpty(accessToken))
        {
            _logger.LogInformation("Apple Music search skipped: no user token");
            return null;
        }

        var query = Uri.EscapeDataString($"{name} {artist}");
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.music.apple.com/v1/catalog/us/search?types=songs&limit=1&term={query}");
        request.Headers.Add("Authorization", $"Bearer {_developerToken}");
        request.Headers.Add("Music-User-Token", accessToken);

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Apple Music search failed for {Name} - {Artist}: HTTP {Status}",
                name, artist, (int)response.StatusCode);
            return null;
        }

        dynamic? obj = JsonConvert.DeserializeObject(content);

        var songs = obj?.results?.songs?.data;
        if (songs == null || !songs.HasValues)
            return null;

        var song = songs[0];
        string songId = song.id.ToString();
        string? imageUrl = song.attributes?.artwork?.url?.ToString()
            ?.Replace("{w}", "300").Replace("{h}", "300");
        int durationMs = (int)(song.attributes?.durationInMillis ?? 0);

        return new TrackModel
        {
            Name = name,
            Artist = artist,
            Platform = Platform.AppleMusic,
            PlatformTrackId = songId,
            ImageUrl = imageUrl,
            DurationMs = durationMs
        };
    }

    public async Task<List<SearchResultTrack>> SearchTracks(string query, int limit, string? accessToken = null)
    {
        var results = new List<SearchResultTrack>();
        if (string.IsNullOrEmpty(accessToken)) return results;

        var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.music.apple.com/v1/catalog/us/search?types=songs&limit={limit}&term={Uri.EscapeDataString(query)}");
        request.Headers.Add("Authorization", $"Bearer {_developerToken}");
        request.Headers.Add("Music-User-Token", accessToken);

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Apple Music search failed for {Query}: HTTP {Status}", query, (int)response.StatusCode);
            return results;
        }

        dynamic? obj = JsonConvert.DeserializeObject(content);
        var songs = obj?.results?.songs?.data;
        if (songs == null) return results;

        foreach (var song in songs)
        {
            string? imageUrl = song.attributes?.artwork?.url?.ToString()
                ?.Replace("{w}", "300").Replace("{h}", "300");
            results.Add(new SearchResultTrack
            {
                PlatformTrackId = song.id.ToString(),
                Name = song.attributes?.name?.ToString() ?? "",
                Artist = song.attributes?.artistName?.ToString() ?? "",
                Album = song.attributes?.albumName?.ToString(),
                ImageUrl = imageUrl,
                DurationMs = (int)(song.attributes?.durationInMillis ?? 0),
                Explicit = song.attributes?.contentRating?.ToString() == "explicit",
                Platform = "AppleMusic"
            });
        }

        return results;
    }

    public async Task<(string Url, string? ImageUrl)> CreatePlaylist(IEnumerable<string> trackIds, string accessToken, string? name = null)
    {
        var trackData = trackIds.Select(id => new
        {
            id,
            type = "songs"
        }).ToList();

        var createData = new
        {
            attributes = new
            {
                name = name ?? $"iko — {DateTime.UtcNow:u}",
                description = "Created with iko"
            },
            relationships = new
            {
                tracks = new { data = trackData }
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post,
            "https://api.music.apple.com/v1/me/library/playlists");
        request.Headers.Add("Authorization", $"Bearer {_developerToken}");
        request.Headers.Add("Music-User-Token", accessToken);
        request.Content = new StringContent(
            JsonConvert.SerializeObject(createData), System.Text.Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new PlatformApiException(Platform.AppleMusic,
                "Failed to create Apple Music playlist", (int)response.StatusCode);

        dynamic? obj = JsonConvert.DeserializeObject(content);

        string playlistId = obj?.data?[0]?.id?.ToString() ?? "";
        string playlistUrl = $"https://music.apple.com/library/playlist/{playlistId}";
        string? imageUrl = obj?.data?[0]?.attributes?.artwork?.url?.ToString()
            ?.Replace("{w}", "300").Replace("{h}", "300");

        return (playlistUrl, imageUrl);
    }

    public async Task<(string Id, string DisplayName)> GetCurrentUser(string userToken)
    {
        // Apple Music API doesn't expose user profile directly
        // Return a placeholder derived from the token being valid
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                "https://api.music.apple.com/v1/me/library/playlists?limit=1");
            request.Headers.Add("Authorization", $"Bearer {_developerToken}");
            request.Headers.Add("Music-User-Token", userToken);

            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
                return ("apple-user", "Apple Music User");

            return ("", "");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Apple Music user check failed");
            return ("", "");
        }
    }

    public async Task<List<PlaylistSummary>> GetPlaylists(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            "https://api.music.apple.com/v1/me/library/playlists?limit=100");
        request.Headers.Add("Authorization", $"Bearer {_developerToken}");
        request.Headers.Add("Music-User-Token", accessToken);

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new PlatformApiException(Platform.AppleMusic,
                "Failed to load Apple Music playlists", (int)response.StatusCode);

        dynamic? obj = JsonConvert.DeserializeObject(content);

        var playlists = new List<PlaylistSummary>();
        if (obj?.data == null) return playlists;

        foreach (var item in obj.data)
        {
            string? imageUrl = null;
            if (item.attributes?.artwork?.url != null)
                imageUrl = item.attributes.artwork.url.ToString()
                    .Replace("{w}", "300").Replace("{h}", "300");

            playlists.Add(new PlaylistSummary
            {
                Id = item.id.ToString(),
                Name = item.attributes?.name?.ToString() ?? "",
                ImageUrl = imageUrl,
                TrackCount = 0
            });
        }

        return playlists;
    }

    public async Task<List<LibraryTrack>> GetPlaylistTracks(string playlistId, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.music.apple.com/v1/me/library/playlists/{playlistId}/tracks");
        request.Headers.Add("Authorization", $"Bearer {_developerToken}");
        request.Headers.Add("Music-User-Token", accessToken);

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new PlatformApiException(Platform.AppleMusic,
                "Failed to load Apple Music playlist tracks", (int)response.StatusCode);

        dynamic? obj = JsonConvert.DeserializeObject(content);

        var tracks = new List<LibraryTrack>();
        if (obj?.data == null) return tracks;

        foreach (var item in obj.data)
        {
            string? imageUrl = null;
            if (item.attributes?.artwork?.url != null)
                imageUrl = item.attributes.artwork.url.ToString()
                    .Replace("{w}", "300").Replace("{h}", "300");

            tracks.Add(new LibraryTrack
            {
                PlatformTrackId = item.id.ToString(),
                Name = item.attributes?.name?.ToString() ?? "",
                Artist = item.attributes?.artistName?.ToString() ?? "",
                ImageUrl = imageUrl,
                DurationMs = (int)(item.attributes?.durationInMillis ?? 0),
                Platform = "AppleMusic"
            });
        }

        return tracks;
    }
}
