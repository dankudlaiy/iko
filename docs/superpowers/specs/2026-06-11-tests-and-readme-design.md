# iko — backend/frontend tests and README (design)

Date: 2026-06-11. Status: approved by user.

## Goal

Add the missing quality layer for the diploma project: a backend test project
(unit + integration), passing Angular tests with a couple of service specs, and a
proper root README with an architecture diagram and setup instructions.

## 1. Backend test project `iko-host.Tests`

- xUnit project at `iko-host.Tests/`, added to `iko.sln`, referencing `iko-host`.
- Packages: `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `Moq`,
  `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.EntityFrameworkCore.Sqlite`.
- `Program.cs` gets `public partial class Program { }` (required by
  `WebApplicationFactory` with top-level statements).

### Refactor for testability: `PlaylistExportService`

The export matching logic currently lives inline in `IkoPlaylistsController.Export`.
Extract it into `iko-host/Services/PlaylistExportService.cs`:

```csharp
public record ExportOutcome(
    string Url, string? ImageUrl, int MatchedCount, int TotalCount,
    List<UnmatchedTrack> UnmatchedTracks);

public record UnmatchedTrack(string Name, string Artist);

public class PlaylistExportService
{
    // ctor: PlatformClientFactory, ILogger<PlaylistExportService>
    public Task<ExportOutcome?> ExportAsync(
        IReadOnlyList<IkoPlaylistTrack> tracks, string playlistName,
        Platform targetPlatform, string accessToken);
    // Returns null when no tracks could be matched.
}
```

The controller keeps request validation / account lookup and delegates matching +
creation to the service. Registered as transient in DI.

### Unit tests

- `PlatformClientFactoryTests`: resolves Spotify/YouTube/AppleMusic; throws
  `UnsupportedPlatformException` for SoundCloud/Deezer.
- `PlaylistExportServiceTests` (Moq `IPlatformClient` via a factory built on mocks):
  same-platform tracks skip search and use `PlatformTrackId` directly; cross-platform
  tracks are searched; unmatched tracks are collected; `CreatePlaylist` receives the
  matched ids in order; returns null when nothing matches (CreatePlaylist not called).
- `SpotifyClientTests`, `YouTubeClientTests`, `AppleMusicClientTests` (mocked
  `HttpMessageHandler`): search result parsing; search returns null on non-success
  HTTP; `GetPlaylists`/`GetPlaylistTracks` throw `PlatformApiException` on
  non-success; YouTube falls back to API key without a token and returns null with
  neither; Apple Music search returns null without a user token. Client constructors
  read env vars — tests set fake values.
- `GlobalExceptionHandlerTests`: `UnsupportedPlatformException` → 400,
  `PlatformApiException` → 502, anything else → 500; body is the `{data, error}`
  envelope.

### Integration tests

`CustomWebApplicationFactory`: sets fake platform env vars, replaces the DbContext
registration with SQLite in-memory (single open connection per factory), runs
migrations.

- `AuthApiTests`: register → 200 with token; duplicate email → 409; invalid email /
  short password → 400 (DataAnnotations); login with wrong password → 401; `/me`
  with token → 200, without → 401.
- `IkoPlaylistsApiTests`: endpoints require auth (401); create → list → get round
  trip; add track; duplicate track → 409; reorder; delete; export to a platform
  that is not connected → 400.

No test ever calls a real external API.

## 2. Angular tests

- Make `ng test --watch=false --browsers=ChromeHeadless` pass.
- Keep/fix the two existing specs (app, header).
- Add `api.service.spec.ts` (HttpTestingController): request URLs and bodies for
  key methods incl. `exportIkoPlaylist`; `platformIndex` mapping.
- Add `auth.service.spec.ts`: login stores the JWT and emits the user; logout clears
  the token and navigates to /login.

## 3. README and .env.example

- Root `README.md` (English): project description and features; mermaid
  architecture diagram (SPA → API → platform clients → external APIs, SQLite);
  tech stack; repository structure; Getting Started (prerequisites, env var table,
  backend + frontend run steps); Docker section (build/run of the existing
  `iko-host/Dockerfile`); Testing section (both test suites); brief API endpoint
  table; Future work.
- `.env.example` with placeholder values for all required env vars.
- Replace the boilerplate `iko-web/README.md` with a short pointer to the root README.

## Out of scope

Secret rotation (deployment stage), new features, frontend component tests beyond
the listed specs.

## Verification

`dotnet test` green; `ng test` green in headless Chrome; `dotnet build` + `ng build`
still green; README renders correctly (mermaid fence, tables).
