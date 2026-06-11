# Design: Player volume + iko-playlist covers (mosaic & custom upload)

Date: 2026-06-04
Status: Approved

## Context

iko lets users build "iko playlists" and play them via an in-app player (Spotify Web Playback SDK, YouTube IFrame, Apple MusicKit). Three gaps to close:

1. **Volume control** — the player has play/seek/shuffle/repeat but no volume.
2. **Default playlist covers** — iko playlists show a generic icon; they should show a mosaic of their tracks' cover art.
3. **Custom covers** — users should be able to upload their own cover image for an iko playlist.

Backend facts (verified): `IkoPlaylist.CoverUrl` (nullable) already exists and is unused; each `IkoPlaylistTrack` stores `ImageUrl`; DB uses `EnsureCreated()` (no migrations), so **no schema change** is required. There is currently **no** static-file serving / `wwwroot` / upload handling. Auth is JWT; controllers get the user via `GetUserId()`. The frontend is Angular 20 + spartan/ui; dev API `http://localhost:5000/api`, prod relative `/api`.

## Precedence rule (used everywhere a cover is shown)

`CoverUrl` set → **custom image**; else ≥1 track image → **2×2 mosaic**; else → **placeholder** (lucide `listMusic` on muted bg).

## Shared frontend: `PlaylistCoverComponent` (`app-playlist-cover`)

- Inputs: `coverUrl: string | null`, `images: string[]`, and a size/class passthrough.
- Renders per the precedence rule. Mosaic = CSS `grid grid-cols-2 grid-rows-2` of the first 4 `images` (`object-cover`); 1 image → single fill; 2–3 → grid with available cells.
- `mediaUrl(path)` helper resolves `CoverUrl`: absolute URL → as-is; otherwise `apiOrigin + path`, where `apiOrigin = environment.apiUrl.replace(/\/api\/?$/, '')` (prod `/api` → `''` → same-origin `/uploads/...`).

## Feature 1 — Volume

- `PlayerState` += `volume: number` (0–1), `isMuted: boolean`. Init from `localStorage` `iko_volume` (default 1) / `iko_muted`.
- `PlayerService`:
  - `setVolume(v)` — clamp 0–1, persist, apply effective volume.
  - `toggleMute()` — persist, apply.
  - `private applyVolume()` — effective = `isMuted ? 0 : volume`; route by current platform: Spotify `spotifyPlayer.setVolume(eff)`, YouTube `ytPlayer.setVolume(eff*100)`, Apple `musicKitInstance.volume = eff`.
  - Call `applyVolume()` at the end of each engine's track-load path so new tracks honor the level.
- UI (player-bar, right of progress, `hidden sm:flex`): mute icon-button (`lucideVolume2` / `lucideVolume1` / `lucideVolumeX` by level) + slim native `<input type=range>` (`accent-primary`) bound to `setVolume`.

## Feature 2 — Default mosaic cover

- Backend: extend **GET `/api/iko-playlists`** list projection to also return `coverImages: string[]` = first 4 non-null track `ImageUrl`s ordered by `Order`. (Single-playlist GET already returns tracks with `ImageUrl`.)
- Frontend: `ApiService.getIkoPlaylists()` consumers read `coverImages`; library cards render `<app-playlist-cover [coverUrl]="pl.coverUrl" [images]="pl.coverImages">`.

## Feature 3 — Custom cover upload

- Backend (`Program.cs`): create `wwwroot/uploads/covers/`, add `app.UseStaticFiles()`.
- Backend (`IkoPlaylistsController`):
  - `POST /api/iko-playlists/{id}/cover` — `IFormFile file`; validate content-type ∈ {image/png, image/jpeg, image/webp} and size ≤ 5 MB; save to `wwwroot/uploads/covers/{playlistId}{ext}`; delete any prior file for that playlist; set `CoverUrl = "/uploads/covers/{file}"`, bump `UpdatedAt`; return the updated playlist (incl. `CoverUrl`). User-scoped (404 if not owner).
  - `DELETE /api/iko-playlists/{id}/cover` — delete file, set `CoverUrl = null` (reverts to mosaic).
  - Optional cache-bust: append `?v={ticks}` to returned `CoverUrl` so the browser refetches after replace.
- Frontend:
  - `ApiService.uploadPlaylistCover(id, file)` → `POST` FormData; `removePlaylistCover(id)` → `DELETE`.
  - Playlist-editor header: add a cover thumbnail (`app-playlist-cover`, ~72px) with a hover "change cover" overlay (camera icon) opening a hidden `<input type=file accept="image/*">`; when `CoverUrl` is set, show a small "remove" control. On success, refresh `playlist`.

## Out of scope (future)

Server-generated mosaic PNG (for sharing/export), drag-to-reorder cover crop, multiple custom covers/history.

## Verification

- Backend builds & runs; `POST`/`DELETE` cover endpoints work (curl with a token); uploaded file served at `/uploads/covers/...`; list endpoint returns `coverImages`.
- Frontend prod build green. Visual check (headless Chrome) of: player volume slider + mute (desktop), library cards showing mosaics, editor custom-cover upload + revert, dark mode.
