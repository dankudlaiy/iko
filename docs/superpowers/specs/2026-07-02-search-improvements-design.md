# iko — improved track search (design)

Date: 2026-07-02. Status: approved by user.

## Goal

Make the track search in the playlist editor feel like real platform search: many
results per platform (not 1), richer metadata, tabbed layout, preview playback, and
proper loading/empty/error states.

## Current limitation

`SearchController` calls each client's `SearchForTrack(q, "", token)` which is
hard-limited to a single best match (`limit=1` / `maxResults=1`). That method is also
used by `PlaylistExportService` for track matching, so it must keep returning one
result. The search UI therefore needs a separate multi-result method.

## Backend

- New model `SearchResultTrack`: `PlatformTrackId, Name, Artist, Album (string?),
  ImageUrl (string?), DurationMs, Explicit (bool), Platform (string)`.
- New interface method `IPlatformClient.SearchTracks(string query, int limit, string? accessToken)`
  returning `List<SearchResultTrack>`. Implemented by all three clients:
  - **Spotify** `/v1/search?type=track&limit={limit}` — maps name, first artist,
    `album.name`, album image, `duration_ms`, `explicit`. Returns `[]` on non-success
    (logs a warning) — search must degrade gracefully, not throw.
  - **YouTube** `/search?part=snippet&type=video&videoCategoryId=10&maxResults={limit}`
    then one batched `/videos?part=contentDetails` call for durations (reuse the
    existing `FetchDurations`). `Album` null, `Explicit` false. `[]` on non-success.
  - **AppleMusic** `/v1/catalog/us/search?types=songs&limit={limit}` (kept for interface
    completeness even though the UI no longer calls it; returns `[]` without a user token).
- `SearchForTrack` (single-result, export matching) is unchanged.
- `SearchController` calls `SearchTracks(q, 15, token)` per requested platform, in the
  existing parallel-with-pre-refreshed-tokens pattern, and returns
  `{ "Spotify": [...], "YouTube": [...] }` (same envelope shape as today).

## Frontend (editor search panel)

- Extend `SearchTrack` in `models.ts` with `album?: string` and `explicit?: boolean`.
- Tabbed results: `All | Spotify | YouTube`, with per-tab counts (e.g. `Spotify (15)`).
  - `All` interleaves platforms round-robin (Spotify, YouTube, Spotify, …).
  - Only render tabs when there are results.
- Result row: cover (larger, ~size-12) · name · `artist · album` · platform badge ·
  duration · `E` explicit tag (when true) · ▶ preview · `+` add (or `✓` when the track
  is already in the iko playlist — dedupe via existing `isTrackInPlaylist`).
- **Preview** (▶): plays the track through the existing `PlayerService.playTrack(...)`
  (same engine as playlist playback; no new audio element). Spotify needs Premium/SDK,
  YouTube plays as usual — consistent with the rest of the app.
- States: skeleton rows while `searchLoading`; "No results for “{query}”" when a
  completed search is empty; an error line when the request fails. Debounce stays 400 ms.

## Out of scope (YAGNI)

- Keyboard navigation (not requested).
- Pagination / "load more" (15 per platform is enough for now).
- Apple Music (already removed from the UI).
- A dedicated 30s preview stream (`preview_url`): reuse the existing player instead.

## Testing

- Unit tests for `SearchTracks` parsing on Spotify and YouTube clients (multi-result,
  non-success → empty). `SearchForTrack` tests remain unchanged.
- `dotnet test` and `ng build` green; deploy via `./deploy.sh`.

## Verification

Search a common term, confirm ~15 results per platform, tab switching, explicit tags,
preview playback, and add/added states; confirm empty and error states.
