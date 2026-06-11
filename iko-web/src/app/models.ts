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

export interface ConnectUrlResult {
  url?: string;
  developerToken?: string;
}

export interface AccessTokenResult {
  accessToken: string;
}

export interface AppleConfig {
  developerToken: string;
}
