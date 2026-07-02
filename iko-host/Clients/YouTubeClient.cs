namespace iko_host.Clients;

using iko_host.Exceptions;
using Models;
using Newtonsoft.Json;

public class YouTubeClient : IPlatformClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<YouTubeClient> _logger;

    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string? _apiKey;

    public YouTubeClient(HttpClient httpClient, ILogger<YouTubeClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _clientId = Environment.GetEnvironmentVariable("YOUTUBE_CLIENT_ID") ??
                    throw new InvalidOperationException("YOUTUBE_CLIENT_ID not found in environment");
        _clientSecret = Environment.GetEnvironmentVariable("YOUTUBE_CLIENT_SECRET") ??
                        throw new InvalidOperationException("YOUTUBE_CLIENT_SECRET not found in environment");
        _apiKey = Environment.GetEnvironmentVariable("YOUTUBE_API_KEY");
    }

    public Platform Platform => Platform.YouTube;

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

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("YouTube search failed for {Name} - {Artist}: HTTP {Status}",
                name, artist, (int)response.StatusCode);
            return null;
        }

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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch YouTube duration for video {VideoId}", videoId);
        }

        return new TrackModel
        {
            Name = name,
            Artist = artist,
            Platform = Platform.YouTube,
            PlatformTrackId = videoId,
            ImageUrl = imageUrl,
            DurationMs = durationMs
        };
    }

    public async Task<List<SearchResultTrack>> SearchTracks(string query, int limit, string? accessToken = null)
    {
        var q = Uri.EscapeDataString(query);
        string url;
        if (!string.IsNullOrEmpty(accessToken))
            url = $"https://www.googleapis.com/youtube/v3/search?part=snippet&type=video&videoCategoryId=10&maxResults={limit}&q={q}";
        else if (!string.IsNullOrEmpty(_apiKey))
            url = $"https://www.googleapis.com/youtube/v3/search?part=snippet&type=video&videoCategoryId=10&maxResults={limit}&q={q}&key={_apiKey}";
        else
            return new List<SearchResultTrack>();

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrEmpty(accessToken))
            request.Headers.Add("Authorization", $"Bearer {accessToken}");

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        var results = new List<SearchResultTrack>();
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("YouTube search failed for {Query}: HTTP {Status}", query, (int)response.StatusCode);
            return results;
        }

        dynamic? obj = JsonConvert.DeserializeObject(content);
        if (obj?.items == null) return results;

        var raw = new List<(string id, string name, string artist, string? img)>();
        foreach (var item in obj.items)
        {
            string videoId = item.id?.videoId?.ToString() ?? "";
            if (string.IsNullOrEmpty(videoId)) continue;
            string? img = item.snippet?.thumbnails?.medium?.url?.ToString();
            raw.Add((videoId,
                item.snippet?.title?.ToString() ?? "",
                item.snippet?.channelTitle?.ToString() ?? "",
                img));
        }

        var durations = await FetchDurations(raw.Select(r => r.id), accessToken);

        foreach (var r in raw)
            results.Add(new SearchResultTrack
            {
                PlatformTrackId = r.id,
                Name = r.name,
                Artist = r.artist,
                Album = null,
                ImageUrl = r.img,
                DurationMs = durations.GetValueOrDefault(r.id, 0),
                Explicit = false,
                Platform = "YouTube"
            });

        return results;
    }

    public async Task<(string Url, string? ImageUrl)> CreatePlaylist(IEnumerable<string> trackIds, string accessToken, string? name = null)
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

        if (!createResponse.IsSuccessStatusCode)
            throw new PlatformApiException(Platform.YouTube,
                "Failed to create YouTube playlist", (int)createResponse.StatusCode);

        dynamic? playlist = JsonConvert.DeserializeObject(createContent);

        string playlistId = playlist!.id.ToString();
        string playlistUrl = $"https://www.youtube.com/playlist?list={playlistId}";
        string? imageUrl = playlist.snippet?.thumbnails?.medium?.url?.ToString();

        // Add videos
        foreach (var videoId in trackIds)
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

    public async Task<OAuthTokenResponse?> ObtainAccessToken(string code, string redirectUri)
    {
        var tokenRequest = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("code", code),
            new KeyValuePair<string, string>("client_id", _clientId),
            new KeyValuePair<string, string>("client_secret", _clientSecret),
            new KeyValuePair<string, string>("redirect_uri", redirectUri),
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

    public async Task<List<PlaylistSummary>> GetPlaylists(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            "https://www.googleapis.com/youtube/v3/playlists?part=snippet,contentDetails&mine=true&maxResults=50");
        request.Headers.Add("Authorization", $"Bearer {accessToken}");

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new PlatformApiException(Platform.YouTube,
                "Failed to load YouTube playlists", (int)response.StatusCode);

        dynamic? obj = JsonConvert.DeserializeObject(content);

        var playlists = new List<PlaylistSummary>();
        if (obj?.items == null) return playlists;

        foreach (var item in obj.items)
        {
            string? imageUrl = null;
            if (item.snippet?.thumbnails?.medium != null)
                imageUrl = item.snippet.thumbnails.medium.url.ToString();

            playlists.Add(new PlaylistSummary
            {
                Id = item.id.ToString(),
                Name = item.snippet?.title?.ToString() ?? "",
                ImageUrl = imageUrl,
                TrackCount = (int)(item.contentDetails?.itemCount ?? 0)
            });
        }

        return playlists;
    }

    public async Task<List<LibraryTrack>> GetPlaylistTracks(string playlistId, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://www.googleapis.com/youtube/v3/playlistItems?part=snippet,contentDetails&playlistId={playlistId}&maxResults=50");
        request.Headers.Add("Authorization", $"Bearer {accessToken}");

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new PlatformApiException(Platform.YouTube,
                "Failed to load YouTube playlist tracks", (int)response.StatusCode);

        dynamic? obj = JsonConvert.DeserializeObject(content);

        if (obj?.items == null) return new List<LibraryTrack>();

        var raw = new List<(string videoId, string name, string artist, string? imageUrl)>();
        foreach (var item in obj.items)
        {
            string? imageUrl = item.snippet?.thumbnails?.medium != null
                ? item.snippet.thumbnails.medium.url.ToString()
                : null;
            string videoId = item.contentDetails?.videoId?.ToString()
                             ?? item.snippet?.resourceId?.videoId?.ToString() ?? "";
            raw.Add((videoId,
                item.snippet?.title?.ToString() ?? "",
                item.snippet?.videoOwnerChannelTitle?.ToString() ?? "",
                imageUrl));
        }

        var durations = await FetchDurations(raw.Select(r => r.videoId), accessToken);

        return raw.Select(r => new LibraryTrack
        {
            PlatformTrackId = r.videoId,
            Name = r.name,
            Artist = r.artist,
            ImageUrl = r.imageUrl,
            DurationMs = durations.GetValueOrDefault(r.videoId, 0),
            Platform = "YouTube"
        }).ToList();
    }

    private async Task<Dictionary<string, int>> FetchDurations(IEnumerable<string> videoIds, string? accessToken)
    {
        var result = new Dictionary<string, int>();
        var ids = videoIds.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        if (ids.Count == 0) return result;

        for (int i = 0; i < ids.Count; i += 50)
        {
            var batch = string.Join(",", ids.Skip(i).Take(50));
            try
            {
                var url = $"https://www.googleapis.com/youtube/v3/videos?part=contentDetails&id={batch}";
                if (string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(_apiKey))
                    url += $"&key={_apiKey}";

                var req = new HttpRequestMessage(HttpMethod.Get, url);
                if (!string.IsNullOrEmpty(accessToken))
                    req.Headers.Add("Authorization", $"Bearer {accessToken}");

                var res = await _httpClient.SendAsync(req);
                dynamic? data = JsonConvert.DeserializeObject(await res.Content.ReadAsStringAsync());

                if (data?.items != null)
                    foreach (var item in data.items)
                    {
                        string id = item.id?.ToString() ?? "";
                        string iso = item.contentDetails?.duration?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(iso))
                            try
                            {
                                result[id] = (int)System.Xml.XmlConvert.ToTimeSpan(iso).TotalMilliseconds;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to parse YouTube duration {Iso} for video {VideoId}", iso, id);
                            }
                    }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch YouTube durations batch starting at {Index}", i);
            }
        }

        return result;
    }
}
