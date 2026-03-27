namespace iko_host.Clients;

using System.Net.Http.Headers;
using Models;
using Newtonsoft.Json;

public class YouTubeClient
{
    public const string RedirectUri = "http://127.0.0.1:5000/api/accounts/callback/youtube";

    private readonly HttpClient _httpClient = new();

    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string? _apiKey;

    public YouTubeClient()
    {
        _clientId = Environment.GetEnvironmentVariable("YOUTUBE_CLIENT_ID") ??
                    throw new InvalidOperationException("YOUTUBE_CLIENT_ID not found in environment");
        _clientSecret = Environment.GetEnvironmentVariable("YOUTUBE_CLIENT_SECRET") ??
                        throw new InvalidOperationException("YOUTUBE_CLIENT_SECRET not found in environment");
        _apiKey = Environment.GetEnvironmentVariable("YOUTUBE_API_KEY");
    }

    /// <summary>
    /// Search for a track. Uses OAuth token if provided, otherwise falls back to API key.
    /// </summary>
    public async Task<TrackModel?> SearchForTrack(string name, string artist, string? accessToken = null)
    {
        var query = Uri.EscapeDataString($"{name} {artist}".Trim());

        string url;
        if (!string.IsNullOrEmpty(accessToken))
        {
            url = $"https://www.googleapis.com/youtube/v3/search?part=snippet&type=video&videoCategoryId=10&maxResults=1&q={query}";
        }
        else if (!string.IsNullOrEmpty(_apiKey))
        {
            url = $"https://www.googleapis.com/youtube/v3/search?part=snippet&type=video&videoCategoryId=10&maxResults=1&q={query}&key={_apiKey}";
        }
        else
        {
            return null;
        }

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrEmpty(accessToken))
            request.Headers.Add("Authorization", $"Bearer {accessToken}");

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        dynamic? obj = JsonConvert.DeserializeObject(content);

        if (obj?.items == null || !obj.items.HasValues)
            return null;

        var item = obj.items[0];
        string videoId = item.id?.videoId?.ToString() ?? "";
        string? imageUrl = item.snippet?.thumbnails?.medium?.url?.ToString();

        // Get video duration via videos endpoint
        int durationMs = 0;
        try
        {
            var durationUrl = !string.IsNullOrEmpty(accessToken)
                ? $"https://www.googleapis.com/youtube/v3/videos?part=contentDetails&id={videoId}"
                : $"https://www.googleapis.com/youtube/v3/videos?part=contentDetails&id={videoId}&key={_apiKey}";

            var durationReq = new HttpRequestMessage(HttpMethod.Get, durationUrl);
            if (!string.IsNullOrEmpty(accessToken))
                durationReq.Headers.Add("Authorization", $"Bearer {accessToken}");

            var durationRes = await _httpClient.SendAsync(durationReq);
            var durationContent = await durationRes.Content.ReadAsStringAsync();
            dynamic? durationObj = JsonConvert.DeserializeObject(durationContent);

            if (durationObj?.items != null && durationObj.items.HasValues)
            {
                string isoDuration = durationObj.items[0].contentDetails.duration.ToString();
                durationMs = (int)System.Xml.XmlConvert.ToTimeSpan(isoDuration).TotalMilliseconds;
            }
        }
        catch
        {
            // Duration is non-critical
        }

        return new TrackModel
        {
            Name = name,
            Artist = artist,
            YouTubeVideoId = videoId,
            ImageUrl = imageUrl,
            DurationMs = durationMs,
            Matched = true
        };
    }

    public async Task<List<TrackModel>> ParsePlaylist(string playlistId, string accessToken)
    {
        var tracks = new List<TrackModel>();
        string? pageToken = null;

        do
        {
            var url = $"https://www.googleapis.com/youtube/v3/playlistItems?part=snippet,contentDetails&playlistId={playlistId}&maxResults=50";
            if (pageToken != null)
                url += $"&pageToken={pageToken}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", $"Bearer {accessToken}");

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            dynamic? obj = JsonConvert.DeserializeObject(content);

            if (obj?.items == null) break;

            foreach (var item in obj.items)
            {
                string? imageUrl = null;
                if (item.snippet?.thumbnails?.medium != null)
                    imageUrl = item.snippet.thumbnails.medium.url.ToString();

                string videoId = item.contentDetails?.videoId?.ToString()
                                 ?? item.snippet?.resourceId?.videoId?.ToString() ?? "";

                tracks.Add(new TrackModel
                {
                    Name = item.snippet?.title?.ToString() ?? "",
                    Artist = item.snippet?.videoOwnerChannelTitle?.ToString() ?? "",
                    YouTubeVideoId = videoId,
                    ImageUrl = imageUrl,
                    DurationMs = 0,
                    Matched = true
                });
            }

            pageToken = obj.nextPageToken?.ToString();
        } while (pageToken != null);

        return tracks;
    }

    public async Task<(string Url, string? ImageUrl)> CreatePlaylist(IEnumerable<string> videoIds, string accessToken, string? name = null)
    {
        // Create playlist
        var createData = new
        {
            snippet = new
            {
                title = name ?? $"iko — {DateTime.UtcNow:u}",
                description = "Created with iko"
            },
            status = new { privacyStatus = "private" }
        };

        var createRequest = new HttpRequestMessage(HttpMethod.Post,
            "https://www.googleapis.com/youtube/v3/playlists?part=snippet,status");
        createRequest.Headers.Add("Authorization", $"Bearer {accessToken}");
        createRequest.Content = new StringContent(
            JsonConvert.SerializeObject(createData), System.Text.Encoding.UTF8, "application/json");

        var createResponse = await _httpClient.SendAsync(createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        dynamic? playlist = JsonConvert.DeserializeObject(createContent);

        string playlistId = playlist!.id.ToString();
        string playlistUrl = $"https://www.youtube.com/playlist?list={playlistId}";
        string? imageUrl = playlist.snippet?.thumbnails?.medium?.url?.ToString();

        // Add videos
        foreach (var videoId in videoIds)
        {
            var addData = new
            {
                snippet = new
                {
                    playlistId,
                    resourceId = new
                    {
                        kind = "youtube#video",
                        videoId
                    }
                }
            };

            var addRequest = new HttpRequestMessage(HttpMethod.Post,
                "https://www.googleapis.com/youtube/v3/playlistItems?part=snippet");
            addRequest.Headers.Add("Authorization", $"Bearer {accessToken}");
            addRequest.Content = new StringContent(
                JsonConvert.SerializeObject(addData), System.Text.Encoding.UTF8, "application/json");

            await _httpClient.SendAsync(addRequest);
            await Task.Delay(300); // Rate limit
        }

        return (playlistUrl, imageUrl);
    }

    public async Task<OAuthTokenResponse?> ObtainAccessToken(string code)
    {
        var tokenRequest = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("code", code),
            new KeyValuePair<string, string>("client_id", _clientId),
            new KeyValuePair<string, string>("client_secret", _clientSecret),
            new KeyValuePair<string, string>("redirect_uri", RedirectUri),
            new KeyValuePair<string, string>("grant_type", "authorization_code")
        });

        var response = await _httpClient.PostAsync("https://oauth2.googleapis.com/token", tokenRequest);
        var json = await response.Content.ReadAsStringAsync();

        return JsonConvert.DeserializeObject<OAuthTokenResponse>(json);
    }

    public async Task<OAuthTokenResponse?> RefreshAccessToken(string refreshToken)
    {
        var tokenRequest = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("client_id", _clientId),
            new KeyValuePair<string, string>("client_secret", _clientSecret),
            new KeyValuePair<string, string>("refresh_token", refreshToken),
            new KeyValuePair<string, string>("grant_type", "refresh_token")
        });

        var response = await _httpClient.PostAsync("https://oauth2.googleapis.com/token", tokenRequest);
        var json = await response.Content.ReadAsStringAsync();

        return JsonConvert.DeserializeObject<OAuthTokenResponse>(json);
    }

    public async Task<(string Id, string DisplayName)> GetCurrentUser(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            "https://www.googleapis.com/youtube/v3/channels?part=snippet&mine=true");
        request.Headers.Add("Authorization", $"Bearer {accessToken}");

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        dynamic? obj = JsonConvert.DeserializeObject(content);

        if (obj?.items != null && obj.items.HasValues)
        {
            string id = obj.items[0].id.ToString();
            string title = obj.items[0].snippet?.title?.ToString() ?? "";
            return (id, title);
        }

        return ("", "");
    }

    public async Task<List<object>> GetPlaylists(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            "https://www.googleapis.com/youtube/v3/playlists?part=snippet,contentDetails&mine=true&maxResults=50");
        request.Headers.Add("Authorization", $"Bearer {accessToken}");

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        dynamic? obj = JsonConvert.DeserializeObject(content);

        var playlists = new List<object>();
        if (obj?.items == null) return playlists;

        foreach (var item in obj.items)
        {
            string? imageUrl = null;
            if (item.snippet?.thumbnails?.medium != null)
                imageUrl = item.snippet.thumbnails.medium.url.ToString();

            playlists.Add(new
            {
                id = item.id.ToString(),
                name = item.snippet?.title?.ToString() ?? "",
                imageUrl,
                trackCount = (int)(item.contentDetails?.itemCount ?? 0)
            });
        }

        return playlists;
    }

    public async Task<List<object>> GetPlaylistTracks(string playlistId, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://www.googleapis.com/youtube/v3/playlistItems?part=snippet,contentDetails&playlistId={playlistId}&maxResults=50");
        request.Headers.Add("Authorization", $"Bearer {accessToken}");

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        dynamic? obj = JsonConvert.DeserializeObject(content);

        var tracks = new List<object>();
        if (obj?.items == null) return tracks;

        foreach (var item in obj.items)
        {
            string? imageUrl = null;
            if (item.snippet?.thumbnails?.medium != null)
                imageUrl = item.snippet.thumbnails.medium.url.ToString();

            string videoId = item.contentDetails?.videoId?.ToString()
                             ?? item.snippet?.resourceId?.videoId?.ToString() ?? "";

            tracks.Add(new
            {
                platformTrackId = videoId,
                name = item.snippet?.title?.ToString() ?? "",
                artist = item.snippet?.videoOwnerChannelTitle?.ToString() ?? "",
                imageUrl,
                durationMs = 0,
                platform = "YouTube"
            });
        }

        return tracks;
    }
}
