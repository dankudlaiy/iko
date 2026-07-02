# Improved Search Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Multi-result, tabbed, richer track search in the playlist editor that feels like real platform search.

**Architecture:** New `IPlatformClient.SearchTracks(query, limit, token)` returns a list (parallel to the existing single-result `SearchForTrack` used by export matching). `SearchController` returns `{platform: [tracks]}`. The editor renders tabs (All/Spotify/YouTube), rich rows, preview via the existing `PlayerService`, and loading/empty/error states.

**Tech Stack:** ASP.NET Core 8, Angular 20, xUnit.

**Spec:** `docs/superpowers/specs/2026-07-02-search-improvements-design.md`

---

### Task 1: `SearchResultTrack` model + interface method

**Files:**
- Create: `iko-host/Models/SearchResultTrack.cs`
- Modify: `iko-host/Clients/IPlatformClient.cs`

- [ ] **Step 1: Model**
```csharp
namespace iko_host.Models;

public class SearchResultTrack
{
    public string PlatformTrackId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string? Album { get; set; }
    public string? ImageUrl { get; set; }
    public int DurationMs { get; set; }
    public bool Explicit { get; set; }
    public string Platform { get; set; } = string.Empty;
}
```

- [ ] **Step 2: Interface** — add to `IPlatformClient`:
```csharp
Task<List<SearchResultTrack>> SearchTracks(string query, int limit, string? accessToken = null);
```

- [ ] **Step 3:** Build will fail until all clients implement it (Tasks 2-4). Commit after Task 4.

---

### Task 2: `SpotifyClient.SearchTracks`

**Files:**
- Modify: `iko-host/Clients/SpotifyClient.cs`

- [ ] **Step 1: Add method** (after `SearchForTrack`):
```csharp
public async Task<List<SearchResultTrack>> SearchTracks(string query, int limit, string? accessToken = null)
{
    accessToken ??= await GetAccessToken();

    var request = new HttpRequestMessage(HttpMethod.Get,
        $"https://api.spotify.com/v1/search?type=track&limit={limit}&q={Uri.EscapeDataString(query)}");
    request.Headers.Add("Authorization", $"Bearer {accessToken}");

    var response = await _httpClient.SendAsync(request);
    var content = await response.Content.ReadAsStringAsync();

    var results = new List<SearchResultTrack>();
    if (!response.IsSuccessStatusCode)
    {
        _logger.LogWarning("Spotify search failed for {Query}: HTTP {Status}", query, (int)response.StatusCode);
        return results;
    }

    dynamic? obj = JsonConvert.DeserializeObject(content);
    var items = obj?.tracks?.items;
    if (items == null) return results;

    foreach (var t in items)
    {
        var artists = new List<string>();
        if (t.artists != null)
            foreach (var a in t.artists) artists.Add(a.name.ToString());

        string? imageUrl = null;
        if (t.album?.images != null && t.album.images.HasValues)
            imageUrl = t.album.images[0].url.ToString();

        results.Add(new SearchResultTrack
        {
            PlatformTrackId = t.id.ToString(),
            Name = t.name.ToString(),
            Artist = string.Join(", ", artists),
            Album = t.album?.name?.ToString(),
            ImageUrl = imageUrl,
            DurationMs = (int)(t.duration_ms ?? 0),
            Explicit = (bool)(t.@explicit ?? false),
            Platform = "Spotify"
        });
    }

    return results;
}
```
Note: `t.@explicit` is the verbatim identifier for the JSON property `explicit` (a C# keyword) on the dynamic object.

---

### Task 3: `YouTubeClient.SearchTracks` (+ API-key duration fallback)

**Files:**
- Modify: `iko-host/Clients/YouTubeClient.cs`

- [ ] **Step 1: Let `FetchDurations` fall back to the API key** when no OAuth token. Change its signature to `string? accessToken` and the request build to:
```csharp
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
                        try { result[id] = (int)System.Xml.XmlConvert.ToTimeSpan(iso).TotalMilliseconds; }
                        catch (Exception ex) { _logger.LogWarning(ex, "Failed to parse YouTube duration {Iso} for {VideoId}", iso, id); }
                }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch YouTube durations batch starting at {Index}", i);
        }
    }

    return result;
}
```

- [ ] **Step 2: Add `SearchTracks`:**
```csharp
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
        raw.Add((videoId, item.snippet?.title?.ToString() ?? "", item.snippet?.channelTitle?.ToString() ?? "", img));
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
```

---

### Task 4: `AppleMusicClient.SearchTracks` (interface completeness)

**Files:**
- Modify: `iko-host/Clients/AppleMusicClient.cs`

- [ ] **Step 1: Add method:**
```csharp
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
        string? imageUrl = song.attributes?.artwork?.url?.ToString()?.Replace("{w}", "300").Replace("{h}", "300");
        results.Add(new SearchResultTrack
        {
            PlatformTrackId = song.id.ToString(),
            Name = song.attributes?.name?.ToString() ?? "",
            Artist = song.attributes?.artistName?.ToString() ?? "",
            Album = song.attributes?.albumName?.ToString(),
            ImageUrl = imageUrl,
            DurationMs = (int)(song.attributes?.durationInMillis ?? 0),
            Explicit = (song.attributes?.contentRating?.ToString() == "explicit"),
            Platform = "AppleMusic"
        });
    }

    return results;
}
```

- [ ] **Step 2: Build** `dotnet build iko-host/iko-host.csproj` → 0 errors.
- [ ] **Step 3: Commit** `git commit -m "feat: SearchTracks (multi-result) on platform clients"`

---

### Task 5: `SearchController` returns multiple results

**Files:**
- Modify: `iko-host/Controllers/SearchController.cs`

- [ ] **Step 1:** Replace the single-result search block. Inside the parallel `searches` select, replace the try body with:
```csharp
try
{
    var token = tokens.GetValueOrDefault(platform);
    var tracks = await _clients.Get(platform).SearchTracks(q, 15, token);
    found.AddRange(tracks.Select(t => (object)new
    {
        platformTrackId = t.PlatformTrackId,
        name = t.Name,
        artist = t.Artist,
        album = t.Album,
        imageUrl = t.ImageUrl,
        durationMs = t.DurationMs,
        @explicit = t.Explicit
    }));
}
catch (Exception ex)
{
    _logger.LogWarning(ex, "Search on {Platform} failed for query {Query}", platform, q);
}
```
(`@explicit` serializes to the JSON key `explicit`.)

- [ ] **Step 2: Build + Commit**
```bash
dotnet build iko-host/iko-host.csproj
git commit -am "feat: search endpoint returns multiple results per platform"
```

---

### Task 6: Client unit tests for `SearchTracks`

**Files:**
- Modify: `iko-host.Tests/Unit/SpotifyClientTests.cs`
- Modify: `iko-host.Tests/Unit/YouTubeClientTests.cs`

- [ ] **Step 1: Spotify test:**
```csharp
[Fact]
public async Task SearchTracks_returns_multiple_with_album_and_explicit()
{
    var handler = StubHttpMessageHandler.RespondingWith(HttpStatusCode.OK,
        """{"tracks":{"items":[
          {"id":"a","name":"Song A","duration_ms":200000,"explicit":true,"album":{"name":"Alb","images":[{"url":"https://img/a"}]},"artists":[{"name":"X"}]},
          {"id":"b","name":"Song B","duration_ms":180000,"explicit":false,"album":{"name":"Alb2","images":[]},"artists":[{"name":"Y"},{"name":"Z"}]}
        ]}}""");

    var res = await Client(handler).SearchTracks("q", 15, "user-token");

    Assert.Equal(2, res.Count);
    Assert.Equal("a", res[0].PlatformTrackId);
    Assert.Equal("Alb", res[0].Album);
    Assert.True(res[0].Explicit);
    Assert.Equal("Y, Z", res[1].Artist);
    Assert.Equal("Spotify", res[0].Platform);
}

[Fact]
public async Task SearchTracks_returns_empty_on_http_error()
{
    var handler = StubHttpMessageHandler.RespondingWith(HttpStatusCode.TooManyRequests, "{}");
    Assert.Empty(await Client(handler).SearchTracks("q", 15, "user-token"));
}
```

- [ ] **Step 2: YouTube test:**
```csharp
[Fact]
public async Task SearchTracks_returns_multiple_results()
{
    var handler = new StubHttpMessageHandler(req =>
        req.RequestUri!.AbsolutePath.Contains("/videos")
            ? StubHttpMessageHandler.Json(HttpStatusCode.OK,
                """{"items":[{"id":"v1","contentDetails":{"duration":"PT3M"}},{"id":"v2","contentDetails":{"duration":"PT2M"}}]}""")
            : StubHttpMessageHandler.Json(HttpStatusCode.OK,
                """{"items":[
                  {"id":{"videoId":"v1"},"snippet":{"title":"T1","channelTitle":"C1","thumbnails":{"medium":{"url":"https://img/1"}}}},
                  {"id":{"videoId":"v2"},"snippet":{"title":"T2","channelTitle":"C2","thumbnails":{"medium":{"url":"https://img/2"}}}}
                ]}"""));

    var res = await Client(handler).SearchTracks("q", 15, "oauth-token");

    Assert.Equal(2, res.Count);
    Assert.Equal("v1", res[0].PlatformTrackId);
    Assert.Equal("C1", res[0].Artist);
    Assert.Equal(180000, res[0].DurationMs);
    Assert.Equal("YouTube", res[0].Platform);
}
```

- [ ] **Step 3: Run + Commit**
```bash
dotnet test iko-host.Tests/iko-host.Tests.csproj
git commit -am "test: SearchTracks parsing for Spotify and YouTube"
```

---

### Task 7: Frontend model + component logic

**Files:**
- Modify: `iko-web/src/app/models.ts`
- Modify: `iko-web/src/app/playlist-editor/playlist-editor.component.ts`

- [ ] **Step 1: Extend `SearchTrack`** in `models.ts`:
```typescript
export interface SearchTrack {
  platformTrackId: string;
  name: string;
  artist: string;
  album?: string;
  imageUrl: string | null;
  durationMs: number;
  explicit?: boolean;
}
```

- [ ] **Step 2: Component state + helpers.** In `playlist-editor.component.ts`:
- Add fields near the search state:
```typescript
searchTab: 'all' | 'Spotify' | 'YouTube' = 'all';
searchError = false;
searchHasQuery = false;
```
- In `performSearch`, set flags:
```typescript
private performSearch(query: string): void {
  this.searchLoading = true;
  this.searchError = false;
  this.searchHasQuery = true;
  this.api.searchAllPlatforms(query).subscribe({
    next: res => {
      this.searchResults = res.data ?? {};
      this.searchLoading = false;
      if (this.searchTab !== 'all' && (this.searchResults[this.searchTab]?.length ?? 0) === 0) {
        this.searchTab = 'all';
      }
    },
    error: () => {
      this.searchResults = {};
      this.searchLoading = false;
      this.searchError = true;
    }
  });
}
```
- Update the debounce subscription (constructor) to clear the query flag when emptied:
```typescript
.subscribe(query => {
  if (query.trim()) {
    this.performSearch(query);
  } else {
    this.searchResults = {};
    this.searchHasQuery = false;
    this.searchError = false;
  }
});
```
- Replace `searchPlatforms()` with count/visibility helpers and a flattened display list:
```typescript
searchCount(platform: string): number {
  return this.searchResults[platform]?.length ?? 0;
}

get totalSearchResults(): number {
  return Object.values(this.searchResults).reduce((n, list) => n + (list?.length ?? 0), 0);
}

displayedResults(): { track: SearchTrack; platform: string }[] {
  if (this.searchTab !== 'all') {
    return (this.searchResults[this.searchTab] ?? []).map(t => ({ track: t, platform: this.searchTab }));
  }
  const platforms = ['Spotify', 'YouTube'];
  const lists = platforms.map(p => (this.searchResults[p] ?? []).map(t => ({ track: t, platform: p })));
  const out: { track: SearchTrack; platform: string }[] = [];
  const max = Math.max(0, ...lists.map(l => l.length));
  for (let i = 0; i < max; i++) {
    for (const l of lists) if (l[i]) out.push(l[i]);
  }
  return out;
}
```
- Add a preview method:
```typescript
previewSearchTrack(track: SearchTrack, platform: string): void {
  this.player.playTrack({
    platformTrackId: track.platformTrackId,
    name: track.name,
    artist: track.artist,
    imageUrl: track.imageUrl,
    durationMs: track.durationMs,
    platform: this.mapPlatform(platform.toLowerCase())
  });
}
```
- Remove the now-unused `searchPlatforms()` method.
- Add `lucidePlay` is already imported; ensure `lucidePlay` stays in imports (used for preview + Play All).

---

### Task 8: Frontend search template

**Files:**
- Modify: `iko-web/src/app/playlist-editor/playlist-editor.component.html`

- [ ] **Step 1:** Replace the entire search-results block (the `@if (searchQuery.trim())` section) with:
```html
@if (searchQuery.trim()) {
  <div class="mb-6">
    <h4 class="mb-2 text-sm font-semibold text-muted-foreground">Search Results</h4>

    <!-- Tabs -->
    @if (totalSearchResults > 0) {
      <div class="mb-3 flex items-center gap-1 border-b">
        @for (tab of ['all', 'Spotify', 'YouTube']; track tab) {
          <button
            type="button"
            class="-mb-px border-b-2 px-3 py-1.5 text-sm font-medium transition-colors"
            [class.border-primary]="searchTab === tab"
            [class.text-foreground]="searchTab === tab"
            [class.border-transparent]="searchTab !== tab"
            [class.text-muted-foreground]="searchTab !== tab"
            (click)="searchTab = $any(tab)"
          >
            {{ tab === 'all' ? 'All' : tab }}@if (tab !== 'all') {<span class="ml-1 text-xs text-muted-foreground">{{ searchCount(tab) }}</span>}
          </button>
        }
      </div>
    }

    <!-- Loading skeletons -->
    @if (searchLoading) {
      @for (i of [0,1,2,3,4]; track i) {
        <div class="flex items-center gap-3 p-2">
          <div hlmSkeleton class="size-12 rounded"></div>
          <div class="flex flex-1 flex-col gap-1.5">
            <div hlmSkeleton class="h-3 w-2/3"></div>
            <div hlmSkeleton class="h-2.5 w-2/5"></div>
          </div>
        </div>
      }
    } @else if (searchError) {
      <p class="px-1 py-6 text-center text-sm text-muted-foreground">Search failed. Try again.</p>
    } @else if (totalSearchResults === 0) {
      <p class="px-1 py-6 text-center text-sm text-muted-foreground">No results for “{{ searchQuery }}”.</p>
    } @else {
      @for (r of displayedResults(); track r.platform + r.track.platformTrackId) {
        <div class="group flex items-center gap-3 rounded-md p-2 transition-colors hover:bg-accent">
          <div class="relative size-12 shrink-0">
            <img [src]="r.track.imageUrl || 'https://via.placeholder.com/48?text=♪'" alt="" class="size-12 rounded object-cover" />
            <button
              type="button"
              class="absolute inset-0 flex items-center justify-center rounded bg-black/50 text-white opacity-0 transition-opacity group-hover:opacity-100"
              (click)="previewSearchTrack(r.track, r.platform)"
              aria-label="Preview"
            >
              <ng-icon hlm name="lucidePlay" />
            </button>
          </div>
          <div class="flex min-w-0 flex-1 flex-col">
            <span class="flex items-center gap-1.5 truncate text-sm font-medium">
              {{ r.track.name }}
              @if (r.track.explicit) {
                <span class="rounded-sm bg-muted px-1 text-[0.625rem] font-semibold text-muted-foreground">E</span>
              }
            </span>
            <span class="truncate text-xs text-muted-foreground">
              {{ r.track.artist }}@if (r.track.album) { · {{ r.track.album }}}
            </span>
          </div>
          <app-platform-badge [platform]="r.platform" size="sm" />
          <span class="text-xs text-muted-foreground">{{ formatDuration(r.track.durationMs) }}</span>
          @if (isTrackInPlaylist(r.track.platformTrackId)) {
            <ng-icon hlm name="lucideCheck" class="text-primary" />
          } @else {
            <button hlmBtn variant="ghost" size="icon" aria-label="Add track" (click)="addTrack(r.track, r.platform)">
              <ng-icon hlm name="lucidePlus" />
            </button>
          }
        </div>
      }
    }
  </div>
}
```

- [ ] **Step 2: Build** `cd iko-web && npx ng build --configuration production` → success.

---

### Task 9: Full verify, deploy

**Files:** none

- [ ] **Step 1:** `dotnet test iko-host.Tests/iko-host.Tests.csproj` → all green.
- [ ] **Step 2:** `cd iko-web && npx ng build` → success.
- [ ] **Step 3: Commit + deploy**
```bash
git add -A
git commit -m "feat: tabbed multi-result search with rich rows, preview, states"
./deploy.sh
```
- [ ] **Step 4: Manual check** on the live site: search a common term, verify ~15 results/platform, tab switching + counts, explicit tags, album names, preview playback, add/added (✓) states, and the empty/error states.
