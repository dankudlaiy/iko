# iko: рефакторинг клиентов, логирование, Export — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Привести iko к «дипломному» качеству: общий интерфейс `IPlatformClient`, Serilog-логирование + глобальный exception handler, фича Export вместо заглушки и удалённого `/convert`, зачистка SoundCloud/Deezer, типизация фронтенда, DataAnnotations, EF-миграции.

**Architecture:** Бэкенд: три клиента платформ реализуют `IPlatformClient`, резолвятся через `PlatformClientFactory`, получают `HttpClient` из `IHttpClientFactory` и `ILogger<T>`. Контроллеры теряют switch-простыни и дублирование. Необработанные исключения ловит `IExceptionHandler` и возвращает существующий конверт `{ data, error }` (не ProblemDetails — фронтенд уже парсит конверт через `err.error?.error`). Фронтенд: единый `models.ts`, типизированный `ApiService`.

**Tech Stack:** ASP.NET Core 8, EF Core 8 + SQLite, Serilog.AspNetCore, Angular 20, spartan/ui.

**Тестирование:** В проекте нет тестовой инфраструктуры для бэкенда; юнит-тесты — отдельный следующий этап по плану пользователя (вне этого объёма). Верификация каждой задачи: `dotnet build` (iko-host), `npx ng build` (iko-web), в конце — ручная проверка ключевых флоу. Спека: `docs/superpowers/specs/2026-06-11-cleanup-logging-export-design.md`.

**Важные отклонения от спеки (осознанные):**
- Исключение названо `UnsupportedPlatformException`, а не `PlatformNotSupportedException` — имя заняла `System.PlatformNotSupportedException`.
- Глобальный handler возвращает конверт `{ data, error }` вместо ProblemDetails — консистентность с фронтендом.
- `GetPlaylistTracks` возвращает `List<LibraryTrack>` (а не `List<TrackModel>`): фронтенд ждёт поле `platform` строкой (`"Spotify"`), а `TrackModel` сериализует enum числом.

---

### Task 1: Модели и интерфейс `IPlatformClient`

**Files:**
- Create: `iko-host/Clients/IPlatformClient.cs`
- Create: `iko-host/Models/PlaylistSummary.cs`
- Create: `iko-host/Models/LibraryTrack.cs`
- Create: `iko-host/Exceptions/PlatformApiException.cs`
- Create: `iko-host/Exceptions/UnsupportedPlatformException.cs`
- Modify: `iko-host/Models/TrackModel.cs`

- [ ] **Step 1: Создать модели**

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

`iko-host/Models/TrackModel.cs` — заменить платформо-специфичные id единым полем:
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
(Поля `SpotifyId`, `YouTubeVideoId`, `AppleMusicId`, `Matched` удалить — их потребители переписываются в задачах 2–6 и 8.)

- [ ] **Step 2: Создать исключения**

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

- [ ] **Step 3: Создать интерфейс**

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

- [ ] **Step 4: Сборка НЕ обязана проходить** (потребители старых полей TrackModel чинятся в задачах 2–6; коммит после Task 6)

---

### Task 2: `SpotifyClient` реализует `IPlatformClient`

**Files:**
- Modify: `iko-host/Clients/SpotifyClient.cs`

- [ ] **Step 1: Переписать класс**

Изменения относительно текущего файла:
- `public class SpotifyClient : IPlatformClient`, свойство `public Platform Platform => Platform.Spotify;`
- Конструктор: `public SpotifyClient(HttpClient httpClient, ILogger<SpotifyClient> logger)` — поля `_httpClient`, `_logger`; убрать `new HttpClient()`.
- Удалить метод `ParsePlaylist` (использовался только удаляемым `PlaylistController`).
- `SearchForTrack`: вместо `SpotifyId = trackId, Matched = true` → `Platform = Platform.Spotify, PlatformTrackId = trackId`; добавить проверку статуса ответа:
```csharp
if (!trackResponse.IsSuccessStatusCode)
{
    _logger.LogWarning("Spotify search failed for {Name} - {Artist}: HTTP {Status}",
        name, artist, (int)trackResponse.StatusCode);
    return null;
}
```
- Добавить методы, перенесённые из `LibraryController` (вместо `dynamic`-дублирования там), с проверкой статуса и `PlatformApiException`:
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
- `CreatePlaylist`: сигнатура `(IEnumerable<string> trackIds, string accessToken, string? name = null)` (уже совпадает); пустой `catch` при получении обложки заменить:
```csharp
catch (Exception ex)
{
    _logger.LogWarning(ex, "Failed to fetch cover for created Spotify playlist {PlaylistId}", (string)playlistId);
    return (playlistUrl, null);
}
```
а после `createResponse` добавить проверку:
```csharp
if (!createResponse.IsSuccessStatusCode)
    throw new PlatformApiException(Platform.Spotify,
        "Failed to create Spotify playlist", (int)createResponse.StatusCode);
```
- Остальные методы (`ObtainAccessToken`, `RefreshAccessToken`, `GetCurrentUser`, приватный `GetAccessToken`) — без изменений, кроме использования внедрённого `_httpClient`.
- Добавить `using iko_host.Exceptions;` и `using Microsoft.Extensions.Logging;` (вторая обычно покрыта ImplicitUsings — добавлять только если сборка попросит).

---

### Task 3: `YouTubeClient` реализует `IPlatformClient`

**Files:**
- Modify: `iko-host/Clients/YouTubeClient.cs`

- [ ] **Step 1: Переписать класс**

- `public class YouTubeClient : IPlatformClient`, `public Platform Platform => Platform.YouTube;`
- Конструктор `(HttpClient httpClient, ILogger<YouTubeClient> logger)`.
- Удалить `ParsePlaylist`.
- `SearchForTrack`: `YouTubeVideoId = videoId, Matched = true` → `Platform = Platform.YouTube, PlatformTrackId = videoId`. Пустой `catch` вокруг получения длительности (строки 84–87) заменить на:
```csharp
catch (Exception ex)
{
    _logger.LogWarning(ex, "Failed to fetch YouTube duration for video {VideoId}", videoId);
}
```
- `GetPlaylists`: тип результата `List<object>` → `List<PlaylistSummary>` (поля те же: `Id`, `Name`, `ImageUrl`, `TrackCount`); при `!response.IsSuccessStatusCode` → `throw new PlatformApiException(Platform.YouTube, "Failed to load YouTube playlists", (int)response.StatusCode);`
- `GetPlaylistTracks`: `List<object>` → `List<LibraryTrack>` (поля: `PlatformTrackId = r.videoId`, `Name`, `Artist`, `ImageUrl`, `DurationMs`, `Platform = "YouTube"`); аналогичная проверка статуса.
- `CreatePlaylist`: добавить проверку статуса `createResponse` → `PlatformApiException`.
- `FetchDurations`: оба пустых `catch` заменить:
```csharp
// внутренний (парсинг ISO-длительности)
catch (Exception ex)
{
    _logger.LogWarning(ex, "Failed to parse YouTube duration {Iso} for video {VideoId}", iso, id);
}
// внешний (запрос батча)
catch (Exception ex)
{
    _logger.LogWarning(ex, "Failed to fetch YouTube durations batch starting at {Index}", i);
}
```

---

### Task 4: `AppleMusicClient` реализует `IPlatformClient`

**Files:**
- Modify: `iko-host/Clients/AppleMusicClient.cs`

- [ ] **Step 1: Переписать класс**

- `public class AppleMusicClient : IPlatformClient`, `public Platform Platform => Models.Platform.AppleMusic;` (если имя свойства конфликтует с типом — использовать полное имя типа в свойстве).
- Конструктор `(HttpClient httpClient, ILogger<AppleMusicClient> logger)`.
- Удалить `ParsePlaylist`.
- `SearchForTrack(string name, string artist, string? accessToken = null)`: в начале
```csharp
if (string.IsNullOrEmpty(accessToken))
{
    _logger.LogInformation("Apple Music search skipped: no user token");
    return null;
}
```
далее как сейчас (заголовок `Music-User-Token` = accessToken); результат: `Platform = Models.Platform.AppleMusic, PlatformTrackId = songId`.
- `GetPlaylists` → `List<PlaylistSummary>`, `GetPlaylistTracks` → `List<LibraryTrack>` (`Platform = "AppleMusic"`), с проверками статуса → `PlatformApiException`.
- `CreatePlaylist`: проверка статуса → `PlatformApiException`.
- `GetCurrentUser`: пустой `catch` → `catch (Exception ex) { _logger.LogWarning(ex, "Apple Music user check failed"); return ("", ""); }`

---

### Task 5: `PlatformClientFactory` + DI

**Files:**
- Create: `iko-host/Clients/PlatformClientFactory.cs`
- Modify: `iko-host/Program.cs` (только блок регистраций клиентов)

- [ ] **Step 1: Создать фабрику**

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

- [ ] **Step 2: Обновить регистрации в Program.cs**

Заменить:
```csharp
builder.Services.AddScoped<SpotifyClient>();
builder.Services.AddScoped<YouTubeClient>();
builder.Services.AddScoped<AppleMusicClient>();
```
на:
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

### Task 6: Переписать `LibraryController` и `SearchController` через фабрику

**Files:**
- Modify: `iko-host/Controllers/LibraryController.cs` (полная замена)
- Modify: `iko-host/Controllers/SearchController.cs` (полная замена)

- [ ] **Step 1: Новый LibraryController**

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
(`UnsupportedPlatformException` из фабрики обработает глобальный handler из Task 7 → 400.)

- [ ] **Step 2: Новый SearchController**

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

- [ ] **Step 3: Сборка**

Run: `dotnet build c:\awoq\iko\iko-host\iko-host.csproj`
Expected: ошибки только в `PlaylistController.cs` (старые поля TrackModel) — он удаляется в Task 8. Если хочется зелёной сборки уже сейчас — допустимо перенести удаление `PlaylistController.cs` и `Models/PlaylistModels.cs` из Task 8 в этот шаг (тогда в Task 8 остаётся только Export и стабы).

- [ ] **Step 4: Commit**

```bash
git add -A iko-host
git commit -m "refactor: introduce IPlatformClient with factory, deduplicate library/search controllers"
```

---

### Task 7: Serilog + глобальный exception handler + CORS

**Files:**
- Create: `iko-host/Infrastructure/GlobalExceptionHandler.cs`
- Modify: `iko-host/Program.cs`
- Modify: `iko-host/iko-host.csproj` (пакет)
- Modify: `iko-host/.gitignore` или корневой `.gitignore` (добавить `logs/`)

- [ ] **Step 1: Установить пакет**

Run: `dotnet add c:\awoq\iko\iko-host\iko-host.csproj package Serilog.AspNetCore`
Expected: пакет добавлен в csproj.

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

В начало файла (после `DotNetEnv.Env.Load();`):
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

После `var builder = ...`: `builder.Host.UseSerilog();`

Регистрация handler-а: `builder.Services.AddExceptionHandler<GlobalExceptionHandler>();`

Исправить CORS (убрать противоречие):
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

В пайплайн сразу после `var app = builder.Build();` блока EnsureCreated:
```csharp
app.UseExceptionHandler(_ => { });
app.UseSerilogRequestLogging();
```
И обернуть `app.Run();`:
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

- [ ] **Step 4: gitignore + сборка + ручная проверка**

Добавить `logs/` в `.gitignore`.
Run: `dotnet build c:\awoq\iko\iko-host\iko-host.csproj` → Expected: Build succeeded (либо те же ожидаемые ошибки PlaylistController, если он ещё не удалён).
Запустить хост, дернуть `GET /api/library/playlists/0` без токена → 401; убедиться, что в консоли структурные логи Serilog и появился файл `logs/iko-*.log`.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: serilog logging, global exception handler, fix CORS policy"
```

---

### Task 8: Export-эндпоинт; удалить PlaylistController и стабы

**Files:**
- Delete: `iko-host/Controllers/PlaylistController.cs`
- Delete: `iko-host/Models/PlaylistModels.cs`
- Modify: `iko-host/Controllers/IkoPlaylistsController.cs`
- Modify: `iko-host/Controllers/AccountsController.cs` (удалить два стаба)

- [ ] **Step 1: Удалить мёртвый код**

Удалить `PlaylistController.cs` и `PlaylistModels.cs`. В `AccountsController.cs` удалить методы `ConnectSoundCloud` и `ConnectDeezer` (и комментарий `// --- Stubs ---`).

- [ ] **Step 2: Export-эндпоинт**

В `IkoPlaylistsController`: добавить в конструктор `PlatformClientFactory clients, ILogger<IkoPlaylistsController> logger` (поля `_clients`, `_logger`), `using iko_host.Clients;`.

Новый метод:
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

DTO (в конец файла, рядом с остальными request-классами):
```csharp
public class ExportPlaylistRequest
{
    public Platform TargetPlatform { get; set; }
}
```

- [ ] **Step 3: Сборка**

Run: `dotnet build c:\awoq\iko\iko-host\iko-host.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add -A iko-host
git commit -m "feat: playlist export endpoint; remove convert backend and platform stubs"
```

---

### Task 9: Фронтенд — удалить /convert, зачистить SoundCloud/Deezer

**Files:**
- Delete: `iko-web/src/app/home/` (вся папка)
- Modify: `iko-web/src/app/app.routes.ts`
- Modify: `iko-web/src/app/services/api.service.ts`
- Modify: `iko-web/src/app/settings/settings.component.ts`, `.html`
- Modify: `iko-web/src/app/platform-badge/platform-badge.component.ts`
- Modify: `iko-web/src/app/playlist-editor/playlist-editor.component.ts`

- [ ] **Step 1: Удалить /convert**

Удалить папку `iko-web/src/app/home/`. В `app.routes.ts` убрать импорт `HomeComponent` и строку `{ path: 'convert', component: HomeComponent },`.

- [ ] **Step 2: ApiService**

Удалить методы `parsePlaylist`, `searchTracks`, `createExternalPlaylist` (и комментарий `// Playlist conversion`). В `platformIndex` убрать `soundcloud: 3, deezer: 4`.

- [ ] **Step 3: Settings**

В `settings.component.ts`: удалить поле `stub` из `PlatformConfig`, убрать записи soundcloud/deezer из `platforms`, убрать `if (platform.stub) return;` в `connect()`. В `settings.component.html`: убрать блок `@if (p.stub) { <span hlmBadge ...>Coming Soon</span> } @else if` → начать с `@if (isConnected(p.id))`.

- [ ] **Step 4: platform-badge и playlist-editor**

`platform-badge.component.ts`: удалить упоминания SoundCloud/Deezer (метод-проверку со списком `['SoundCloud', 'Deezer', ...]`, цвета `soundcloud`/`deezer`, ветки `case 'soundcloud'`/`case 'deezer'` и связанный с ними вывод в шаблоне, если он есть).

`playlist-editor.component.ts`: `platformName` → `return ['Spotify', 'YouTube', 'AppleMusic'][index] || '';`

- [ ] **Step 5: Сборка + Commit**

Run: `npx ng build` (из `iko-web`)
Expected: успешная сборка.

```bash
git add -A iko-web
git commit -m "refactor: remove convert page and SoundCloud/Deezer stubs from frontend"
```

---

### Task 10: Типизация фронтенда (`models.ts`)

**Files:**
- Create: `iko-web/src/app/models.ts`
- Modify: `iko-web/src/app/services/api.service.ts`
- Modify: `iko-web/src/app/playlist-editor/playlist-editor.component.ts`
- Modify: `iko-web/src/app/library/library.component.ts`
- Modify: `iko-web/src/app/settings/settings.component.ts`
- Modify: `iko-web/src/app/services/auth.service.ts`, `player.service.ts` (по месту, где есть `any`)

- [ ] **Step 1: Создать models.ts**

```typescript
export interface ApiResponse<T> {
  data: T | null;
  error: string | null;
}

/** Индекс платформы соответствует enum Platform на бэкенде. */
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

- [ ] **Step 2: Типизировать ApiService**

Каждый метод получает конкретный возвращаемый тип, например:
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
По этому образцу — все методы сервиса.

- [ ] **Step 3: Убрать `any` в компонентах**

В `playlist-editor.component.ts`: `playlist: IkoPlaylistDetail | null`, `tracks: IkoPlaylistTrack[]`, `searchResults: SearchResults`, `sourcePlaylists: LibraryPlaylist[]`, `sourcePlaylistTracks: LibraryTrack[]`, `addTrack(track: SearchTrack | LibraryTrack, platform?: string)`, `playTrack(track: IkoPlaylistTrack)`, `toIkoTrack(t: IkoPlaylistTrack)`, `onDrop(event: CdkDragDrop<IkoPlaylistTrack[]>)`. Аналогично пройтись по `library.component.ts`, `settings.component.ts` (`connectedAccounts: ConnectedAccount[]`), `auth.service.ts`, `player.service.ts` — заменить `any` на типы из `models.ts`. Цель: `grep -rn ": any" iko-web/src/app --include="*.ts"` не находит ничего, кроме осознанных мест (SDK сторонних плееров — там допустимо оставить с комментарием).

- [ ] **Step 4: Сборка + Commit**

Run: `npx ng build` → Expected: успех.

```bash
git add -A iko-web
git commit -m "refactor: typed API models, eliminate any in services and components"
```

---

### Task 11: Export UI в редакторе плейлиста

**Files:**
- Modify: `iko-web/src/app/services/api.service.ts`
- Modify: `iko-web/src/app/playlist-editor/playlist-editor.component.ts`
- Modify: `iko-web/src/app/playlist-editor/playlist-editor.component.html`

- [ ] **Step 1: Метод в ApiService**

```typescript
exportIkoPlaylist(playlistId: string, platform: string): Observable<ApiResponse<ExportResult>> {
  return this.http.post<ApiResponse<ExportResult>>(
    `${API_URL}/iko-playlists/${playlistId}/export`,
    { targetPlatform: this.platformIndex(platform) }
  );
}
```

- [ ] **Step 2: Логика в компоненте**

Заменить `exportStub()` на:
```typescript
exporting = false;
exportResult: ExportResult | null = null;

exportTo(platform: string, platformLabel: string): void {
  if (this.exporting) return;
  this.exporting = true;
  this.exportResult = null;
  // getAccountToken обновляет протухший access token на сервере перед экспортом
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
(импортировать `ExportResult` из `../models`).

- [ ] **Step 3: Шаблон**

Дропдаун Export (строки ~206–213) заменить на:
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
Ниже (после блока с кнопками, в подходящем месте карточки плейлиста) — панель результата:
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

- [ ] **Step 4: Сборка + ручная проверка + Commit**

Run: `npx ng build` → успех. Ручная проверка: запустить хост и фронт, экспортировать IKO-плейлист в подключённую платформу, убедиться, что плейлист появился и панель результата показывает ссылку.

```bash
git add -A iko-web
git commit -m "feat: export iko playlist to connected platform from editor"
```

---

### Task 12: DataAnnotations на DTO бэкенда

**Files:**
- Modify: `iko-host/Models/AuthModels.cs`
- Modify: `iko-host/Controllers/IkoPlaylistsController.cs` (request-классы)
- Modify: `iko-host/Controllers/AccountsController.cs` (`AppleMusicTokenRequest`)
- Modify: `iko-host/Controllers/AuthController.cs` (убрать ручные проверки-дубликаты, если есть)

- [ ] **Step 1: Атрибуты**

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

Request-классы `IkoPlaylistsController.cs`:
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
(`using System.ComponentModel.DataAnnotations;` вверху файла.)

`AppleMusicTokenRequest`: `[Required]` на `UserToken`.

В `AuthController` проверить и удалить ручные проверки, которые теперь покрыты атрибутами (пустые email/пароль); проверки уникальности email и корректности пароля при логине остаются.

Примечание: автоматический 400 от `[ApiController]` имеет формат ValidationProblemDetails (не `{data,error}`); фронтенд в этом случае покажет общий fallback-текст тоста — приемлемо.

- [ ] **Step 2: Сборка + Commit**

Run: `dotnet build c:\awoq\iko\iko-host\iko-host.csproj` → успех.

```bash
git add -A iko-host
git commit -m "feat: request validation via data annotations"
```

---

### Task 13: EF-миграции вместо EnsureCreated

**Files:**
- Create: `iko-host/Migrations/*` (генерируется)
- Modify: `iko-host/Program.cs`
- Delete: `iko-host/iko.db` (пересоздание одобрено пользователем)

- [ ] **Step 1: Установить dotnet-ef (если нет)**

Run: `dotnet ef --version`; если команда не найдена: `dotnet tool install --global dotnet-ef`

- [ ] **Step 2: Сгенерировать миграцию**

Run (из `c:\awoq\iko\iko-host`): `dotnet ef migrations add InitialCreate`
Expected: папка `Migrations/` с `*_InitialCreate.cs` и snapshot.

- [ ] **Step 3: Program.cs**

Заменить `db.Database.EnsureCreated();` на `db.Database.Migrate();`

- [ ] **Step 4: Пересоздать БД**

Удалить `iko-host/iko.db` (и `iko.db-shm`/`iko.db-wal`, если есть). Запустить хост: `dotnet run` → Expected: в логах создание таблиц, файл `iko.db` появился, `GET /api/auth`-флоу работает (регистрация нового пользователя через UI).

- [ ] **Step 5: Commit**

```bash
git add -A iko-host
git commit -m "feat: EF Core migrations instead of EnsureCreated"
```

---

### Task 14: Финальная верификация

- [ ] **Step 1: Полная сборка**

Run: `dotnet build c:\awoq\iko\iko-host\iko-host.csproj` и `npx ng build` (из iko-web) → оба успешны.

- [ ] **Step 2: Smoke-тест вручную**

Запустить бэкенд + `npm start` (iko-web). Проверить: регистрация → логин → подключение Spotify/YouTube в Settings → библиотека платформы открывается → создание IKO-плейлиста → добавление треков из поиска и из библиотеки → drag-drop → Export в подключённую платформу → плеер играет трек. Убедиться, что `logs/iko-*.log` пишется и не содержит ERROR при штатной работе.

- [ ] **Step 3: Зачистка `: any`**

Run: `grep -rn ": any" iko-web/src/app --include="*.ts"`
Expected: только осознанные места (типизация SDK сторонних плееров), каждое с понятным контекстом.
