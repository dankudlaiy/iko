namespace iko_host.Clients;

using Models;
using Newtonsoft.Json;

public class AppleMusicClient
{
    private readonly HttpClient _httpClient = new();
    private readonly string _developerToken;

    public AppleMusicClient()
    {
        _developerToken = Environment.GetEnvironmentVariable("APPLE_DEVELOPER_TOKEN") ?? "";
    }

    public string DeveloperToken => _developerToken;

    public async Task<TrackModel?> SearchForTrack(string name, string artist, string userToken)
    {
        var query = Uri.EscapeDataString($"{name} {artist}");
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.music.apple.com/v1/catalog/us/search?types=songs&limit=1&term={query}");
        request.Headers.Add("Authorization", $"Bearer {_developerToken}");
        request.Headers.Add("Music-User-Token", userToken);

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
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
            AppleMusicId = songId,
            ImageUrl = imageUrl,
            DurationMs = durationMs,
            Matched = true
        };
    }

    public async Task<List<TrackModel>> ParsePlaylist(string playlistId, string userToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.music.apple.com/v1/me/library/playlists/{playlistId}/tracks");
        request.Headers.Add("Authorization", $"Bearer {_developerToken}");
        request.Headers.Add("Music-User-Token", userToken);

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        dynamic? obj = JsonConvert.DeserializeObject(content);

        var tracks = new List<TrackModel>();
        if (obj?.data == null) return tracks;

        foreach (var item in obj.data)
        {
            string? imageUrl = null;
            if (item.attributes?.artwork?.url != null)
                imageUrl = item.attributes.artwork.url.ToString()
                    .Replace("{w}", "300").Replace("{h}", "300");

            tracks.Add(new TrackModel
            {
                Name = item.attributes?.name?.ToString() ?? "",
                Artist = item.attributes?.artistName?.ToString() ?? "",
                AppleMusicId = item.id.ToString(),
                ImageUrl = imageUrl,
                DurationMs = (int)(item.attributes?.durationInMillis ?? 0),
                Matched = true
            });
        }

        return tracks;
    }

    public async Task<(string Url, string? ImageUrl)> CreatePlaylist(IEnumerable<string> trackIds, string userToken, string? name = null)
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
        request.Headers.Add("Music-User-Token", userToken);
        request.Content = new StringContent(
            JsonConvert.SerializeObject(createData), System.Text.Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
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
        catch
        {
            return ("", "");
        }
    }

    public async Task<List<object>> GetPlaylists(string userToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            "https://api.music.apple.com/v1/me/library/playlists?limit=100");
        request.Headers.Add("Authorization", $"Bearer {_developerToken}");
        request.Headers.Add("Music-User-Token", userToken);

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        dynamic? obj = JsonConvert.DeserializeObject(content);

        var playlists = new List<object>();
        if (obj?.data == null) return playlists;

        foreach (var item in obj.data)
        {
            string? imageUrl = null;
            if (item.attributes?.artwork?.url != null)
                imageUrl = item.attributes.artwork.url.ToString()
                    .Replace("{w}", "300").Replace("{h}", "300");

            playlists.Add(new
            {
                id = item.id.ToString(),
                name = item.attributes?.name?.ToString() ?? "",
                imageUrl,
                trackCount = 0
            });
        }

        return playlists;
    }

    public async Task<List<object>> GetPlaylistTracks(string playlistId, string userToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.music.apple.com/v1/me/library/playlists/{playlistId}/tracks");
        request.Headers.Add("Authorization", $"Bearer {_developerToken}");
        request.Headers.Add("Music-User-Token", userToken);

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        dynamic? obj = JsonConvert.DeserializeObject(content);

        var tracks = new List<object>();
        if (obj?.data == null) return tracks;

        foreach (var item in obj.data)
        {
            string? imageUrl = null;
            if (item.attributes?.artwork?.url != null)
                imageUrl = item.attributes.artwork.url.ToString()
                    .Replace("{w}", "300").Replace("{h}", "300");

            tracks.Add(new
            {
                platformTrackId = item.id.ToString(),
                name = item.attributes?.name?.ToString() ?? "",
                artist = item.attributes?.artistName?.ToString() ?? "",
                imageUrl,
                durationMs = (int)(item.attributes?.durationInMillis ?? 0),
                platform = "AppleMusic"
            });
        }

        return tracks;
    }
}
