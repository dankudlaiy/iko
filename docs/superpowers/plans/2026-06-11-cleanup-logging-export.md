# iko: platform-client refactor, logging, Export — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bring iko to thesis-grade quality: a shared `IPlatformClient` interface, Serilog logging + a global exception handler, an Export feature replacing the stub and the removed `/convert` page, SoundCloud/Deezer cleanup, frontend typing, DataAnnotations, EF migrations.

**Architecture:** Backend: the three platform clients implement `IPlatformClient`, are resolved via `PlatformClientFactory`, get their `HttpClient` from `IHttpClientFactory` plus `ILogger<T>`. Controllers lose their switch ladders and duplication. Unhandled exceptions are caught by an `IExceptionHandler` that returns the existing `{ data, error }` envelope (not ProblemDetails — the frontend already parses the envelope via `err.error?.error`). Frontend: a single `models.ts`, fully typed `ApiService`.

**Tech Stack:** ASP.NET Core 8, EF Core 8 + SQLite, Serilog.AspNetCore, Angular 20, spartan/ui.

**Testing:** The backend has no test infrastructure; unit tests are a separate upcoming work item (out of scope here). Verification per task: `dotnet build` (iko-host), `npx ng build` (iko-web), plus a manual smoke test of the key flows at the end. Spec: `docs/superpowers/specs/2026-06-11-cleanup-logging-export-design.md`.

**Deliberate deviations from the spec:**
- The exception is named `UnsupportedPlatformException`, not `PlatformNotSupportedException` — the latter clashes with `System.PlatformNotSupportedException`.
- The global handler returns the `{ data, error }` envelope instead of ProblemDetails — consistency with the frontend.
- `GetPlaylistTracks` returns `List<LibraryTrack>` (not `List<TrackModel>`): the frontend expects `platform` as a string (`"Spotify"`), while `TrackModel` serializes the enum as a number.

---

### Task 1: Models and the `IPlatformClient` interface

**Files:**
- Create: `iko-host/Clients/IPlatformClient.cs`
- Create: `iko-host/Models/PlaylistSummary.cs`
- Create: `iko-host/Models/LibraryTrack.cs`
- Create: `iko-host/Exceptions/PlatformApiException.cs`
- Create: `iko-host/Exceptions/UnsupportedPlatformException.cs`
- Modify: `iko-host/Models/TrackModel.cs`

- [x] **Step 1: Create the models**

`iko-host/Models/PlaylistSummary.cs`:
```csharp
namespace iko_host.Models;

public class PlaylistSummary
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int TrackCount { get; set; }
}
```

`iko-host/Models/LibraryTrack.cs`:
```csharp
namespace iko_host.Models;

public class LibraryTrack
{
    public string PlatformTrackId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int DurationMs { get; set; }
    public string Platform { get; set; } = string.Empty; // "Spotify" | "YouTube" | "AppleMusic"
}
```

`iko-host/Models/TrackModel.cs` — replace platform-specific ids with a unified field:
```csharp
namespace iko_host.Models;

public class TrackModel
{
    public string Name { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int DurationMs { get; set; }
    public Platform Platform { get; set; }
    public string? PlatformTrackId { get; set; }
}
```
(Drop `SpotifyId`, `YouTubeVideoId`, `AppleMusicId`, `Matched` — their consumers are rewritten in Tasks 2–6 and 8.)

- [x] **Step 2: Create the exceptions**

`iko-host/Exceptions/PlatformApiException.cs`:
```csharp
namespace iko_host.Exceptions;

using iko_host.Models;

public class PlatformApiException : Exception
{
    public Platform Platform { get; }
    public int? StatusCode { get; }

    public PlatformApiException(Platform platform, string message, int? statusCode = null)
        : base(message)
    {
        Platform = platform;
        StatusCode = statusCode;
    }
}
```

`iko-host/Exceptions/UnsupportedPlatformException.cs`:
```csharp
namespace iko_host.Exceptions;

using iko_host.Models;

public class UnsupportedPlatformException : Exception
{
    public Platform Platform { get; }

    public UnsupportedPlatformException(Platform platform)
        : base($"Platform {platform} is not supported")
    {
        Platform = platform;
    }
}
```

- [x] **Step 3: Create the interface**

`iko-host/Clients/IPlatformClient.cs`:
```csharp
namespace iko_host.Clients;

using iko_host.Models;

public interface IPlatformClient
{
    Platform Platform { get; }

    Task<List<PlaylistSummary>> GetPlaylists(string accessToken);
    Task<List<LibraryTrack>> GetPlaylistTracks(string playlistId, string accessToken);
    Task<TrackModel?> SearchForTrack(string name, string artist, string? accessToken = null);
    Task<(string Url, string? ImageUrl)> CreatePlaylist(IEnumerable<string> trackIds, string accessToken, string? name = null);
}
```

- [x] **Step 4: Build is NOT required to pass yet** (consumers of the old TrackModel fields get fixed in Tasks 2–6; commit happens after Task 6)

---

### Task 2: `SpotifyClient` implements `IPlatformClient`

**Files:**
- Modify: `iko-host/Clients/SpotifyClient.cs`

- [x] **Step 1: Rewrite the class**

Changes relative to the current file:
- `public class SpotifyClient : IPlatformClient`, property `public Platform Platform => Platform.Spotify;`
- Constructor: `public SpotifyClient(HttpClient httpClient, ILogger<SpotifyClient> logger)` — fields `_httpClient`, `_logger`; drop `new HttpClient()`.
- Delete `ParsePlaylist` (used only by the removed `PlaylistController`).
- `SearchForTrack`: `SpotifyId = trackId, Matched = true` → `Platform = Platform.Spotify, PlatformTrackId = trackId`; add a response status check:
```csharp
if (!trackResponse.IsSuccessStatusCode)
{
    _logger.LogWarning("Spotify search failed for {Name} - {Artist}: HTTP {Status}",
        name, artist, (int)trackResponse.StatusCode);
    return null;
}
```
- Add the methods moved from `LibraryController` (replacing the `dynamic` duplication there), with status checks and `PlatformApiException`:
```csharp
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
```
- `CreatePlaylist`: signature `(IEnumerable<string> trackIds, string accessToken, string? name = null)` (already matches); replace the empty cover-fetch `catch`:
```csharp
catch (Exception ex)
{
    _logger.LogWarning(ex, "Failed to fetch cover for created Spotify playlist {PlaylistId}", (string)playlistId);
    return (playlistUrl, null);
}
```
and add a check after `createResponse`:
```csharp
if (!createResponse.IsSuccessStatusCode)
    throw new PlatformApiException(Platform.Spotify,
        "Failed to create Spotify playlist", (int)createResponse.StatusCode);
```
- The remaining methods (`ObtainAccessToken`, `RefreshAccessToken`, `GetCurrentUser`, private `GetAccessToken`) — unchanged except for using the injected `_httpClient`.
- Add `using iko_host.Exceptions;` (and `using Microsoft.Extensions.Logging;` only if the build asks for it — ImplicitUsings usually covers it).

---

### Task 3: `YouTubeClient` implements `IPlatformClient`

**Files:**
- Modify: `iko-host/Clients/YouTubeClient.cs`

- [ ] **Step 1: Rewrite the class**

- `public class YouTubeClient : IPlatformClient`, `public Platform Platform => Platform.YouTube;`
- Constructor `(HttpClient httpClient, ILogger<YouTubeClient> logger)`.
- Delete `ParsePlaylist`.
- `SearchForTrack`: `YouTubeVideoId = videoId, Matched = true` → `Platform = Platform.YouTube, PlatformTrackId = videoId`. Replace the empty `catch` around the duration fetch (old lines 84–87) with:
```csharp
catch (Exception ex)
{
    _logger.LogWarning(ex, "Failed to fetch YouTube duration for video {VideoId}", videoId);
}
```
- `GetPlaylists`: result type `List<object>` → `List<PlaylistSummary>` (same fields: `Id`, `Name`, `ImageUrl`, `TrackCount`); on `!response.IsSuccessStatusCode` → `throw new PlatformApiException(Platform.YouTube, "Failed to load YouTube playlists", (int)response.StatusCode);`
- `GetPlaylistTracks`: `List<object>` → `List<LibraryTrack>` (fields: `PlatformTrackId = r.videoId`, `Name`, `Artist`, `ImageUrl`, `DurationMs`, `Platform = "YouTube"`); same status check.
- `CreatePlaylist`: add a status check on `createResponse` → `PlatformApiException`.
- `FetchDurations`: replace both empty `catch` blocks:
```csharp
// inner (ISO duration parsing)
catch (Exception ex)
{
    _logger.LogWarning(ex, "Failed to parse YouTube duration {Iso} for video {VideoId}", iso, id);
}
// outer (batch request)
catch (Exception ex)
{
    _logger.LogWarning(ex, "Failed to fetch YouTube durations batch starting at {Index}", i);
}
```

---

### Task 4: `AppleMusicClient` implements `IPlatformClient`

**Files:**
- Modify: `iko-host/Clients/AppleMusicClient.cs`

- [ ] **Step 1: Rewrite the class**

- `public class AppleMusicClient : IPlatformClient`, `public Platform Platform => Models.Platform.AppleMusic;` (use the fully-qualified type name if the property name clashes with the type).
- Constructor `(HttpClient httpClient, ILogger<AppleMusicClient> logger)`.
- Delete `ParsePlaylist`.
- `SearchForTrack(string name, string artist, string? accessToken = null)`: at the top
```csharp
if (string.IsNullOrEmpty(accessToken))
{
    _logger.LogInformation("Apple Music search skipped: no user token");
    return null;
}
```
then as before (the `Music-User-Token` header = accessToken); result: `Platform = Models.Platform.AppleMusic, PlatformTrackId = songId`.
- `GetPlaylists` → `List<PlaylistSummary>`, `GetPlaylistTracks` → `List<LibraryTrack>` (`Platform = "AppleMusic"`), with status checks → `PlatformApiException`.
- `CreatePlaylist`: status check → `PlatformApiException`.
- `GetCurrentUser`: empty `catch` → `catch (Exception ex) { _logger.LogWarning(ex, "Apple Music user check failed"); return ("", ""); }`

---

### Task 5: `PlatformClientFactory` + DI

**Files:**
- Create: `iko-host/Clients/PlatformClientFactory.cs`
- Modify: `iko-host/Program.cs` (client registration block only)

- [ ] **Step 1: Create the factory**

```csharp
namespace iko_host.Clients;

using iko_host.Exceptions;
using iko_host.Models;

public class PlatformClientFactory
{
    private readonly IEnumerable<IPlatformClient> _clients;

    public PlatformClientFactory(IEnumerable<IPlatformClient> clients)
    {
        _clients = clients;
    }

    public IPlatformClient Get(Platform platform) =>
        _clients.FirstOrDefault(c => c.Platform == platform)
        ?? throw new UnsupportedPlatformException(platform);
}
```

- [ ] **Step 2: Update registrations in Program.cs**

Replace:
```csharp
builder.Services.AddScoped<SpotifyClient>();
builder.Services.AddScoped<YouTubeClient>();
builder.Services.AddScoped<AppleMusicClient>();
```
with:
```csharp
builder.Services.AddHttpClient<SpotifyClient>();
builder.Services.AddHttpClient<YouTubeClient>();
builder.Services.AddHttpClient<AppleMusicClient>();
builder.Services.AddTransient<IPlatformClient>(sp => sp.GetRequiredService<SpotifyClient>());
builder.Services.AddTransient<IPlatformClient>(sp => sp.GetRequiredService<YouTubeClient>());
builder.Services.AddTransient<IPlatformClient>(sp => sp.GetRequiredService<AppleMusicClient>());
builder.Services.AddTransient<PlatformClientFactory>();
```

---

### Task 6: Rewrite `LibraryController` and `SearchController` via the factory

**Files:**
- Modify: `iko-host/Controllers/LibraryController.cs` (full replacement)
- Modify: `iko-host/Controllers/SearchController.cs` (full replacement)

- [ ] **Step 1: New LibraryController**

```csharp
using System.Security.Claims;
using iko_host.Clients;
using iko_host.Data;
using iko_host.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace iko_host.Controllers;

[ApiController]
[Route("api/library")]
[Authorize]
public class LibraryController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PlatformClientFactory _clients;

    public LibraryController(AppDbContext db, PlatformClientFactory clients)
    {
        _db = db;
        _clients = clients;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("playlists/{platform}")]
    public async Task<IActionResult> GetPlaylists(Platform platform)
    {
        var account = await GetAccount(platform);
        if (account == null)
            return BadRequest(new { data = (object?)null, error = $"{platform} not connected" });

        var playlists = await _clients.Get(platform).GetPlaylists(account.AccessToken);
        return Ok(new { data = playlists, error = (string?)null });
    }

    [HttpGet("playlists/{platform}/{playlistId}/tracks")]
    public async Task<IActionResult> GetPlaylistTracks(Platform platform, string playlistId)
    {
        var account = await GetAccount(platform);
        if (account == null)
            return BadRequest(new { data = (object?)null, error = $"{platform} not connected" });

        var tracks = await _clients.Get(platform).GetPlaylistTracks(playlistId, account.AccessToken);
        return Ok(new { data = tracks, error = (string?)null });
    }

    private async Task<ConnectedAccount?> GetAccount(Platform platform)
    {
        var userId = GetUserId();
        return await _db.ConnectedAccounts.FirstOrDefaultAsync(
            ca => ca.UserId == userId && ca.Platform == platform);
    }
}
```
(`UnsupportedPlatformException` thrown by the factory is mapped to 400 by the global handler from Task 7.)

- [ ] **Step 2: New SearchController**

```csharp
using System.Security.Claims;
using iko_host.Clients;
using iko_host.Data;
using iko_host.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace iko_host.Controllers;

[ApiController]
[Route("api/search")]
[Authorize]
public class SearchController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PlatformClientFactory _clients;
    private readonly ILogger<SearchController> _logger;

    public SearchController(AppDbContext db, PlatformClientFactory clients, ILogger<SearchController> logger)
    {
        _db = db;
        _clients = clients;
        _logger = logger;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] string? platforms = null)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { data = (object?)null, error = "Query is required" });

        var requested = (platforms ?? "Spotify,YouTube,AppleMusic")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => Enum.TryParse<Platform>(p, out _))
            .Select(Enum.Parse<Platform>)
            .ToList();

        var userId = GetUserId();
        var accounts = await _db.ConnectedAccounts
            .Where(ca => ca.UserId == userId && requested.Contains(ca.Platform))
            .ToDictionaryAsync(ca => ca.Platform, ca => ca.AccessToken);

        var results = new Dictionary<string, List<object>>();

        var searches = requested.Select(async platform =>
        {
            var found = new List<object>();
            try
            {
                var token = accounts.GetValueOrDefault(platform);
                var track = await _clients.Get(platform).SearchForTrack(q, "", token);
                if (track?.PlatformTrackId != null)
                {
                    found.Add(new
                    {
                        platformTrackId = track.PlatformTrackId,
                        name = track.Name,
                        artist = track.Artist,
                        imageUrl = track.ImageUrl,
                        durationMs = track.DurationMs
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Search on {Platform} failed for query {Query}", platform, q);
            }
            lock (results) { results[platform.ToString()] = found; }
        });

        await Task.WhenAll(searches);

        return Ok(new { data = results, error = (string?)null });
    }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build c:\awoq\iko\iko-host\iko-host.csproj`
Expected: errors only in `PlaylistController.cs` (old TrackModel fields) — it is deleted in Task 8. If a green build is desired now, it is acceptable to pull the deletion of `PlaylistController.cs` and `Models/PlaylistModels.cs` forward from Task 8 into this step (Task 8 then keeps only Export and the stubs).

- [ ] **Step 4: Commit**

```bash
git add -A iko-host
git commit -m "refactor: introduce IPlatformClient with factory, deduplicate library/search controllers"
```

---

### Task 7: Serilog + global exception handler + CORS

**Files:**
- Create: `iko-host/Infrastructure/GlobalExceptionHandler.cs`
- Modify: `iko-host/Program.cs`
- Modify: `iko-host/iko-host.csproj` (package)
- Modify: `.gitignore` (add `logs/`)

- [ ] **Step 1: Install the package**

Run: `dotnet add c:\awoq\iko\iko-host\iko-host.csproj package Serilog.AspNetCore`
Expected: package added to the csproj.

- [ ] **Step 2: GlobalExceptionHandler**

`iko-host/Infrastructure/GlobalExceptionHandler.cs`:
```csharp
namespace iko_host.Infrastructure;

using iko_host.Exceptions;
using Microsoft.AspNetCore.Diagnostics;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, message) = exception switch
        {
            UnsupportedPlatformException => (StatusCodes.Status400BadRequest, exception.Message),
            PlatformApiException pae => (StatusCodes.Status502BadGateway,
                $"{pae.Platform} API error: {pae.Message}"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };

        _logger.LogError(exception, "Unhandled exception on {Method} {Path}",
            httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(
            new { data = (object?)null, error = message }, cancellationToken);
        return true;
    }
}
```

- [ ] **Step 3: Program.cs**

At the top of the file (after `DotNetEnv.Env.Load();`):
```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine(AppContext.BaseDirectory, "logs", "iko-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14)
    .CreateLogger();
```
usings: `using Serilog;` `using Serilog.Events;` `using iko_host.Infrastructure;`

After `var builder = ...`: `builder.Host.UseSerilog();`

Handler registration: `builder.Services.AddExceptionHandler<GlobalExceptionHandler>();`

Fix CORS (remove the contradiction):
```csharp
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:4200")
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});
```

In the pipeline, right after the `var app = builder.Build();` EnsureCreated block:
```csharp
app.UseExceptionHandler(_ => { });
app.UseSerilogRequestLogging();
```
And wrap `app.Run();`:
```csharp
try
{
    app.Run();
}
finally
{
    Log.CloseAndFlush();
}
```

- [ ] **Step 4: gitignore + build + manual check**

Add `logs/` to `.gitignore`.
Run: `dotnet build c:\awoq\iko\iko-host\iko-host.csproj` → Expected: Build succeeded (or the same expected PlaylistController errors if it has not been deleted yet).
Start the host, hit `GET /api/library/playlists/0` without a token → 401; confirm structured Serilog output in the console and a `logs/iko-*.log` file.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: serilog logging, global exception handler, fix CORS policy"
```

---

### Task 8: Export endpoint; delete PlaylistController and stubs

**Files:**
- Delete: `iko-host/Controllers/PlaylistController.cs`
- Delete: `iko-host/Models/PlaylistModels.cs`
- Modify: `iko-host/Controllers/IkoPlaylistsController.cs`
- Modify: `iko-host/Controllers/AccountsController.cs` (delete the two stubs)

- [ ] **Step 1: Delete dead code**

Delete `PlaylistController.cs` and `PlaylistModels.cs`. In `AccountsController.cs` delete the `ConnectSoundCloud` and `ConnectDeezer` methods (and the `// --- Stubs ---` comment).

- [ ] **Step 2: Export endpoint**

In `IkoPlaylistsController`: add `PlatformClientFactory clients, ILogger<IkoPlaylistsController> logger` to the constructor (fields `_clients`, `_logger`), `using iko_host.Clients;`.

New method:
```csharp
[HttpPost("{id:guid}/export")]
public async Task<IActionResult> Export(Guid id, [FromBody] ExportPlaylistRequest request)
{
    var userId = GetUserId();
    var playlist = await _db.IkoPlaylists
        .Include(p => p.Tracks.OrderBy(t => t.Order))
        .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);

    if (playlist == null)
        return NotFound(new { data = (object?)null, error = "Playlist not found" });
    if (playlist.Tracks.Count == 0)
        return BadRequest(new { data = (object?)null, error = "Playlist is empty" });

    var account = await _db.ConnectedAccounts.FirstOrDefaultAsync(
        ca => ca.UserId == userId && ca.Platform == request.TargetPlatform);
    if (account == null)
        return BadRequest(new { data = (object?)null, error = $"{request.TargetPlatform} is not connected" });

    var client = _clients.Get(request.TargetPlatform);
    var matchedIds = new List<string>();
    var unmatched = new List<object>();

    foreach (var track in playlist.Tracks)
    {
        if (track.Platform == request.TargetPlatform)
        {
            matchedIds.Add(track.PlatformTrackId);
            continue;
        }

        var found = await client.SearchForTrack(track.Name, track.Artist, account.AccessToken);
        if (found?.PlatformTrackId != null)
            matchedIds.Add(found.PlatformTrackId);
        else
            unmatched.Add(new { track.Name, track.Artist });
    }

    if (matchedIds.Count == 0)
        return BadRequest(new { data = (object?)null, error = "No tracks could be matched on the target platform" });

    var (url, imageUrl) = await client.CreatePlaylist(matchedIds, account.AccessToken, playlist.Name);

    _logger.LogInformation(
        "Exported playlist {PlaylistId} to {Platform}: {Matched}/{Total} tracks",
        id, request.TargetPlatform, matchedIds.Count, playlist.Tracks.Count);

    return Ok(new
    {
        data = new
        {
            url,
            imageUrl,
            matchedCount = matchedIds.Count,
            totalCount = playlist.Tracks.Count,
            unmatchedTracks = unmatched
        },
        error = (string?)null
    });
}
```

DTO (at the end of the file, next to the other request classes):
```csharp
public class ExportPlaylistRequest
{
    public Platform TargetPlatform { get; set; }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build c:\awoq\iko\iko-host\iko-host.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add -A iko-host
git commit -m "feat: playlist export endpoint; remove convert backend and platform stubs"
```

---

### Task 9: Frontend — remove /convert, clean up SoundCloud/Deezer

**Files:**
- Delete: `iko-web/src/app/home/` (entire folder)
- Modify: `iko-web/src/app/app.routes.ts`
- Modify: `iko-web/src/app/services/api.service.ts`
- Modify: `iko-web/src/app/settings/settings.component.ts`, `.html`
- Modify: `iko-web/src/app/platform-badge/platform-badge.component.ts`
- Modify: `iko-web/src/app/playlist-editor/playlist-editor.component.ts`

- [ ] **Step 1: Remove /convert**

Delete the `iko-web/src/app/home/` folder. In `app.routes.ts` remove the `HomeComponent` import and the `{ path: 'convert', component: HomeComponent },` line.

- [ ] **Step 2: ApiService**

Delete the `parsePlaylist`, `searchTracks`, `createExternalPlaylist` methods (and the `// Playlist conversion` comment). In `platformIndex` remove `soundcloud: 3, deezer: 4`.

- [ ] **Step 3: Settings**

In `settings.component.ts`: delete the `stub` field from `PlatformConfig`, remove the soundcloud/deezer entries from `platforms`, remove `if (platform.stub) return;` in `connect()`. In `settings.component.html`: remove the `@if (p.stub) { <span hlmBadge ...>Coming Soon</span> } @else if` block → start with `@if (isConnected(p.id))`.

- [ ] **Step 4: platform-badge and playlist-editor**

`platform-badge.component.ts`: remove SoundCloud/Deezer references (the check method with the `['SoundCloud', 'Deezer', ...]` list, the `soundcloud`/`deezer` colors, the `case 'soundcloud'`/`case 'deezer'` branches and any related template output).

`playlist-editor.component.ts`: `platformName` → `return ['Spotify', 'YouTube', 'AppleMusic'][index] || '';`

- [ ] **Step 5: Build + Commit**

Run: `npx ng build` (from `iko-web`)
Expected: build succeeds.

```bash
git add -A iko-web
git commit -m "refactor: remove convert page and SoundCloud/Deezer stubs from frontend"
```

---

### Task 10: Frontend typing (`models.ts`)

**Files:**
- Create: `iko-web/src/app/models.ts`
- Modify: `iko-web/src/app/services/api.service.ts`
- Modify: `iko-web/src/app/playlist-editor/playlist-editor.component.ts`
- Modify: `iko-web/src/app/library/library.component.ts`
- Modify: `iko-web/src/app/settings/settings.component.ts`
- Modify: `iko-web/src/app/services/auth.service.ts`, `player.service.ts` (wherever `any` occurs)

- [ ] **Step 1: Create models.ts**

```typescript
export interface ApiResponse<T> {
  data: T | null;
  error: string | null;
}

/** Platform index matches the backend Platform enum. */
export type PlatformIndex = 0 | 1 | 2; // Spotify | YouTube | AppleMusic
export type PlatformName = 'Spotify' | 'YouTube' | 'AppleMusic';

export interface IkoPlaylistSummary {
  id: string;
  name: string;
  coverUrl: string | null;
  trackCount: number;
  coverImages: string[];
  createdAt: string;
  updatedAt: string;
}

export interface IkoPlaylistTrack {
  id: string;
  platform: PlatformIndex;
  platformTrackId: string;
  name: string;
  artist: string;
  imageUrl: string | null;
  durationMs: number;
  order: number;
  addedAt: string;
}

export interface IkoPlaylistDetail {
  id: string;
  name: string;
  coverUrl: string | null;
  createdAt: string;
  updatedAt: string;
  tracks: IkoPlaylistTrack[];
}

export interface LibraryPlaylist {
  id: string;
  name: string;
  imageUrl: string | null;
  trackCount: number;
}

export interface LibraryTrack {
  platformTrackId: string;
  name: string;
  artist: string;
  imageUrl: string | null;
  durationMs: number;
  platform: PlatformName;
}

export interface SearchTrack {
  platformTrackId: string;
  name: string;
  artist: string;
  imageUrl: string | null;
  durationMs: number;
}

export type SearchResults = Record<string, SearchTrack[]>;

export interface ConnectedAccount {
  platform: PlatformIndex;
  platformUserId: string | null;
  platformDisplayName: string | null;
  expiresAt: string | null;
}

export interface ExportResult {
  url: string;
  imageUrl: string | null;
  matchedCount: number;
  totalCount: number;
  unmatchedTracks: { name: string; artist: string }[];
}

export interface AddTrackBody {
  platform: number;
  platformTrackId: string;
  name: string;
  artist: string;
  imageUrl: string | null;
  durationMs: number;
}
```

- [ ] **Step 2: Type ApiService**

Every method gets a concrete return type, e.g.:
```typescript
getIkoPlaylists(): Observable<ApiResponse<IkoPlaylistSummary[]>> {
  return this.http.get<ApiResponse<IkoPlaylistSummary[]>>(`${API_URL}/iko-playlists`);
}

getIkoPlaylist(id: string): Observable<ApiResponse<IkoPlaylistDetail>> { ... }
addTrackToPlaylist(playlistId: string, track: AddTrackBody): Observable<ApiResponse<IkoPlaylistTrack>> { ... }
getLibraryPlaylists(platform: string): Observable<ApiResponse<LibraryPlaylist[]>> { ... }
getLibraryPlaylistTracks(platform: string, playlistId: string): Observable<ApiResponse<LibraryTrack[]>> { ... }
searchAllPlatforms(query: string, platforms?: string): Observable<ApiResponse<SearchResults>> { ... }
getConnectedAccounts(): Observable<ApiResponse<ConnectedAccount[]>> { ... }
```
Apply this pattern to every method in the service.

- [ ] **Step 3: Eliminate `any` in components**

In `playlist-editor.component.ts`: `playlist: IkoPlaylistDetail | null`, `tracks: IkoPlaylistTrack[]`, `searchResults: SearchResults`, `sourcePlaylists: LibraryPlaylist[]`, `sourcePlaylistTracks: LibraryTrack[]`, `addTrack(track: SearchTrack | LibraryTrack, platform?: string)`, `playTrack(track: IkoPlaylistTrack)`, `toIkoTrack(t: IkoPlaylistTrack)`, `onDrop(event: CdkDragDrop<IkoPlaylistTrack[]>)`. Do the same pass over `library.component.ts`, `settings.component.ts` (`connectedAccounts: ConnectedAccount[]`), `auth.service.ts`, `player.service.ts` — replace `any` with types from `models.ts`. Goal: `grep -rn ": any" iko-web/src/app --include="*.ts"` finds nothing except deliberate spots (third-party player SDK typings — acceptable there, with a comment).

- [ ] **Step 4: Build + Commit**

Run: `npx ng build` → Expected: success.

```bash
git add -A iko-web
git commit -m "refactor: typed API models, eliminate any in services and components"
```

---

### Task 11: Export UI in the playlist editor

**Files:**
- Modify: `iko-web/src/app/services/api.service.ts`
- Modify: `iko-web/src/app/playlist-editor/playlist-editor.component.ts`
- Modify: `iko-web/src/app/playlist-editor/playlist-editor.component.html`

- [ ] **Step 1: ApiService method**

```typescript
exportIkoPlaylist(playlistId: string, platform: string): Observable<ApiResponse<ExportResult>> {
  return this.http.post<ApiResponse<ExportResult>>(
    `${API_URL}/iko-playlists/${playlistId}/export`,
    { targetPlatform: this.platformIndex(platform) }
  );
}
```

- [ ] **Step 2: Component logic**

Replace `exportStub()` with:
```typescript
exporting = false;
exportResult: ExportResult | null = null;

exportTo(platform: string, platformLabel: string): void {
  if (this.exporting) return;
  this.exporting = true;
  this.exportResult = null;
  // getAccountToken refreshes a stale access token server-side before exporting
  this.api.getAccountToken(platform).subscribe({
    next: () => {
      this.api.exportIkoPlaylist(this.playlistId, platform).subscribe({
        next: res => {
          this.exporting = false;
          this.exportResult = res.data;
          toast(`Exported ${res.data?.matchedCount}/${res.data?.totalCount} tracks to ${platformLabel}`);
        },
        error: err => {
          this.exporting = false;
          toast(err.error?.error || 'Export failed');
        }
      });
    },
    error: () => {
      this.exporting = false;
      toast(`${platformLabel} is not connected`);
    }
  });
}
```
(import `ExportResult` from `../models`).

- [ ] **Step 3: Template**

Replace the Export dropdown (around old lines 206–213) with:
```html
<button hlmBtn variant="outline" [disabled]="exporting" [hlmDropdownMenuTrigger]="exportMenu">
  {{ exporting ? 'Exporting…' : 'Export' }}
</button>
<ng-template #exportMenu>
  <div hlmDropdownMenu class="w-48">
    <button hlmDropdownMenuItem (click)="exportTo('spotify', 'Spotify')">Export to Spotify</button>
    <button hlmDropdownMenuItem (click)="exportTo('youtube', 'YouTube')">Export to YouTube</button>
    <button hlmDropdownMenuItem (click)="exportTo('applemusic', 'Apple Music')">Export to Apple Music</button>
  </div>
</ng-template>
```
Below (after the button block, in a suitable spot in the playlist card) — the result panel:
```html
@if (exportResult) {
  <div class="mt-4 rounded-lg border bg-card p-4 text-sm">
    <p class="font-medium">
      Exported {{ exportResult.matchedCount }} of {{ exportResult.totalCount }} tracks.
      <a [href]="exportResult.url" target="_blank" rel="noopener" class="text-primary underline">Open playlist</a>
    </p>
    @if (exportResult.unmatchedTracks.length > 0) {
      <p class="mt-2 text-muted-foreground">Not found on target platform:</p>
      <ul class="mt-1 list-disc pl-5 text-muted-foreground">
        @for (t of exportResult.unmatchedTracks; track $index) {
          <li>{{ t.name }} — {{ t.artist }}</li>
        }
      </ul>
    }
    <button hlmBtn variant="ghost" size="sm" class="mt-2" (click)="exportResult = null">Dismiss</button>
  </div>
}
```

- [ ] **Step 4: Build + manual check + Commit**

Run: `npx ng build` → success. Manual check: run the host and the frontend, export an IKO playlist to a connected platform, confirm the playlist appears and the result panel shows the link.

```bash
git add -A iko-web
git commit -m "feat: export iko playlist to connected platform from editor"
```

---

### Task 12: DataAnnotations on backend DTOs

**Files:**
- Modify: `iko-host/Models/AuthModels.cs`
- Modify: `iko-host/Controllers/IkoPlaylistsController.cs` (request classes)
- Modify: `iko-host/Controllers/AccountsController.cs` (`AppleMusicTokenRequest`)
- Modify: `iko-host/Controllers/AuthController.cs` (drop duplicated manual checks, if any)

- [ ] **Step 1: Attributes**

`AuthModels.cs`:
```csharp
using System.ComponentModel.DataAnnotations;

namespace iko_host.Models;

public class RegisterRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(8)]
    public string Password { get; set; } = string.Empty;
}

public class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
```

Request classes in `IkoPlaylistsController.cs`:
```csharp
public class CreateIkoPlaylistRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
}

public class UpdateIkoPlaylistRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
}

public class AddTrackRequest
{
    public Platform Platform { get; set; }

    [Required, MaxLength(200)]
    public string PlatformTrackId { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(300)]
    public string Artist { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }
    public int DurationMs { get; set; }
}
```
(`using System.ComponentModel.DataAnnotations;` at the top of the file.)

`AppleMusicTokenRequest`: `[Required]` on `UserToken`.

In `AuthController` check for and remove manual checks now covered by attributes (empty email/password); keep email-uniqueness and login password verification.

Note: the automatic 400 from `[ApiController]` uses the ValidationProblemDetails shape (not `{data,error}`); the frontend falls back to a generic toast message in that case — acceptable.

- [ ] **Step 2: Build + Commit**

Run: `dotnet build c:\awoq\iko\iko-host\iko-host.csproj` → success.

```bash
git add -A iko-host
git commit -m "feat: request validation via data annotations"
```

---

### Task 13: EF migrations instead of EnsureCreated

**Files:**
- Create: `iko-host/Migrations/*` (generated)
- Modify: `iko-host/Program.cs`
- Delete: `iko-host/iko.db` (recreation approved by the user)

- [ ] **Step 1: Install dotnet-ef (if missing)**

Run: `dotnet ef --version`; if the command is missing: `dotnet tool install --global dotnet-ef`

- [ ] **Step 2: Generate the migration**

Run (from `c:\awoq\iko\iko-host`): `dotnet ef migrations add InitialCreate`
Expected: a `Migrations/` folder with `*_InitialCreate.cs` and a snapshot.

- [ ] **Step 3: Program.cs**

Replace `db.Database.EnsureCreated();` with `db.Database.Migrate();`

- [ ] **Step 4: Recreate the DB**

Delete `iko-host/iko.db` (and `iko.db-shm`/`iko.db-wal` if present). Start the host: `dotnet run` → Expected: table creation in the logs, `iko.db` recreated, the auth flow works (register a new user through the UI).

- [ ] **Step 5: Commit**

```bash
git add -A iko-host
git commit -m "feat: EF Core migrations instead of EnsureCreated"
```

---

### Task 14: Final verification

- [ ] **Step 1: Full build**

Run: `dotnet build c:\awoq\iko\iko-host\iko-host.csproj` and `npx ng build` (from iko-web) → both succeed.

- [ ] **Step 2: Manual smoke test**

Start the backend + `npm start` (iko-web). Verify: register → login → connect Spotify/YouTube in Settings → platform library opens → create an IKO playlist → add tracks from search and from the library → drag-drop → Export to a connected platform → the player plays a track. Confirm `logs/iko-*.log` is written and contains no ERROR entries during normal operation.

- [ ] **Step 3: `: any` sweep**

Run: `grep -rn ": any" iko-web/src/app --include="*.ts"`
Expected: only deliberate spots (third-party player SDK typings), each with clear context.
