# iko — refactoring, logging, Export, typing (design)

Date: 2026-06-11. Status: approved by user.

## Goal

Bring the project to "thesis-grade" quality: logging and error handling, deduplication
via a shared platform-client interface, replacing stubs with a real Export feature,
frontend typing, server-side validation, EF migrations.

Secret rotation (.env in git) is deferred to the deployment stage and is out of scope here.

## 1. Shared platform-client interface

```csharp
public interface IPlatformClient
{
    Platform Platform { get; }
    Task<List<PlaylistSummary>> GetPlaylists(string accessToken);
    Task<List<TrackModel>> GetPlaylistTracks(string playlistId, string accessToken);
    Task<TrackModel?> SearchForTrack(string name, string artist, string? accessToken);
    Task<(string url, string? imageUrl)> CreatePlaylist(IEnumerable<string> trackIds, string accessToken, string name);
}
```

- Implemented by `SpotifyClient`, `YouTubeClient`, `AppleMusicClient`.
- The duplicated Spotify logic in `LibraryController` (`new HttpClient()`, `dynamic`)
  moves into `SpotifyClient` (`GetPlaylists`, `GetPlaylistTracks`).
- All clients switch to `IHttpClientFactory` (`AddHttpClient<T>()`).
- `PlatformClientFactory` resolves `Platform` → `IPlatformClient`; unsupported
  platforms throw a dedicated exception.
- Controllers (`LibraryController`, the new Export) use the factory instead of switches.
- New `PlaylistSummary` model (id, name, imageUrl, trackCount) replaces anonymous objects.

## 2. Logging and error handling

- **Serilog** (`Serilog.AspNetCore`): console + rolling file `logs/iko-.log`
  (daily), `UseSerilogRequestLogging()`.
- `ILogger<T>` in all clients and controllers.
- Empty `catch { }` blocks in `YouTubeClient` and swallowed exceptions in
  `SearchController` become `LogWarning` with context (platform, track, HTTP status).
  Graceful degradation behavior (one platform failing does not break the search)
  is preserved.
- Global handler: `IExceptionHandler` (.NET 8). Unhandled exception → log + 500.
  `PlatformApiException` (new exception thrown by clients on external API errors)
  → 502 with the platform name.
- Expected controller errors keep the `{ data, error }` envelope.
- Fix CORS in Program.cs: remove the `WithOrigins` + `AllowAnyOrigin` contradiction,
  keep `WithOrigins("http://localhost:4200")`.

## 3. Export instead of /convert

Removed:
- the `/convert` route and `HomeComponent` (ts/html/css);
- `PlaylistController` entirely with its DTOs (`ParseRequest`, `SearchRequest`,
  `CreatePlaylistApiRequest`);
- `parsePlaylist`, `searchTracks`, `createExternalPlaylist` methods in `ApiService`.

Added:
- `POST /api/iko-playlists/{id}/export`, body `{ targetPlatform }`.
  Logic: a track already on the target platform → use its `PlatformTrackId` directly;
  otherwise `SearchForTrack(name, artist)`. Create the playlist via
  `IPlatformClient.CreatePlaylist`.
  Response: `{ url, matchedCount, totalCount, unmatchedTracks }`.
- UI in the playlist editor: Export button → dropdown of connected platforms →
  progress indicator → result panel with the link and the list of unmatched tracks.

## 4. SoundCloud/Deezer cleanup

- Remove from platform lists in settings and from `platform-badge`.
- Delete the `connect/soundcloud`, `connect/deezer` stubs in `AccountsController`.
- Switch branches disappear together with the factory migration.
- `Platform` enum values stay (DB compatibility; described as "future work" in the thesis).

## 5. Server-side validation (DataAnnotations)

- `[Required]`, `[EmailAddress]`, `[MinLength(8)]` (password), `[MaxLength(100)]`
  (playlist names) on request DTOs.
- `[ApiController]` provides automatic 400; remove duplicated manual checks.

## 6. Frontend typing

- `src/app/models.ts`: `Track`, `IkoPlaylist`, `LibraryPlaylist`, `SearchResults`,
  `ConnectedAccount`, `UserInfo`, `ExportResult`,
  `ApiResponse<T> = { data: T | null; error: string | null }`.
- `ApiService` fully typed; `any` in components replaced with interfaces.

## 7. EF migrations

- Create an `InitialCreate` migration; `EnsureCreated()` → `Migrate()` in Program.cs.
- The local `iko.db` is deleted and recreated by the migration (approved by the user —
  local test data is disposable).

## Implementation order

1. Backend refactoring: `IPlatformClient`, move Spotify logic, `IHttpClientFactory`,
   factory, rewrite `LibraryController`.
2. Serilog + `IExceptionHandler` + CORS fix.
3. Export endpoint + remove PlaylistController.
4. Frontend: remove /convert and HomeComponent, Export UI, SoundCloud/Deezer cleanup.
5. Typing (`models.ts`, ApiService, components).
6. DataAnnotations.
7. EF migrations, recreate the dev DB.

After each block — `dotnet build` / `ng build`; at the end — manual verification of
the key flows (library, editor, export, search).
