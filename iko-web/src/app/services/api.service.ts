import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

const API_URL = 'http://localhost:5000/api';

@Injectable({ providedIn: 'root' })
export class ApiService {
  constructor(private http: HttpClient) {}

  // Playlist conversion
  parsePlaylist(platform: string, link: string): Observable<any> {
    return this.http.post(`${API_URL}/playlist/parse`, { platform: this.platformIndex(platform), link });
  }

  searchTracks(tracks: { name: string; artist: string }[], targetPlatform: string): Observable<any> {
    return this.http.post(`${API_URL}/playlist/search`, {
      tracks,
      targetPlatform: this.platformIndex(targetPlatform)
    });
  }

  createExternalPlaylist(targetPlatform: string, trackIds: string[], playlistName: string, accessToken?: string): Observable<any> {
    return this.http.post(`${API_URL}/playlist/create`, {
      targetPlatform: this.platformIndex(targetPlatform),
      trackIds,
      playlistName,
      accessToken
    });
  }

  // Connected accounts
  getConnectedAccounts(): Observable<any> {
    return this.http.get(`${API_URL}/accounts`);
  }

  disconnectAccount(platform: string): Observable<any> {
    return this.http.delete(`${API_URL}/accounts/${this.platformIndex(platform)}`);
  }

  getConnectUrl(platform: string): Observable<any> {
    return this.http.get(`${API_URL}/accounts/connect/${platform.toLowerCase()}`);
  }

  getAccountToken(platform: string): Observable<any> {
    return this.http.get(`${API_URL}/accounts/token/${this.platformIndex(platform)}`);
  }

  // Iko playlists
  getIkoPlaylists(): Observable<any> {
    return this.http.get(`${API_URL}/iko-playlists`);
  }

  createIkoPlaylist(name: string): Observable<any> {
    return this.http.post(`${API_URL}/iko-playlists`, { name });
  }

  getIkoPlaylist(id: string): Observable<any> {
    return this.http.get(`${API_URL}/iko-playlists/${id}`);
  }

  updateIkoPlaylist(id: string, name: string): Observable<any> {
    return this.http.put(`${API_URL}/iko-playlists/${id}`, { name });
  }

  deleteIkoPlaylist(id: string): Observable<any> {
    return this.http.delete(`${API_URL}/iko-playlists/${id}`);
  }

  addTrackToPlaylist(playlistId: string, track: any): Observable<any> {
    return this.http.post(`${API_URL}/iko-playlists/${playlistId}/tracks`, track);
  }

  removeTrackFromPlaylist(playlistId: string, trackId: string): Observable<any> {
    return this.http.delete(`${API_URL}/iko-playlists/${playlistId}/tracks/${trackId}`);
  }

  reorderTracks(playlistId: string, orderedIds: string[]): Observable<any> {
    return this.http.patch(`${API_URL}/iko-playlists/${playlistId}/tracks/reorder`, { orderedIds });
  }

  // Library
  getLibraryPlaylists(platform: string): Observable<any> {
    return this.http.get(`${API_URL}/library/playlists/${this.platformIndex(platform)}`);
  }

  getLibraryPlaylistTracks(platform: string, playlistId: string): Observable<any> {
    return this.http.get(`${API_URL}/library/playlists/${this.platformIndex(platform)}/${playlistId}/tracks`);
  }

  // Search
  searchAllPlatforms(query: string, platforms = 'Spotify,YouTube,AppleMusic'): Observable<any> {
    return this.http.get(`${API_URL}/search`, { params: { q: query, platforms } });
  }

  // Apple config
  getAppleConfig(): Observable<any> {
    return this.http.get(`${API_URL}/config/apple`);
  }

  platformIndex(platform: string): number {
    const platforms: Record<string, number> = {
      spotify: 0,
      youtube: 1,
      applemusic: 2,
      soundcloud: 3,
      deezer: 4
    };
    return platforms[platform.toLowerCase()] ?? 0;
  }
}
