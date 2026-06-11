import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

const API_URL = environment.apiUrl;

@Injectable({ providedIn: 'root' })
export class ApiService {
  constructor(private http: HttpClient) {}

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

  uploadPlaylistCover(playlistId: string, file: File): Observable<any> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post(`${API_URL}/iko-playlists/${playlistId}/cover`, form);
  }

  removePlaylistCover(playlistId: string): Observable<any> {
    return this.http.delete(`${API_URL}/iko-playlists/${playlistId}/cover`);
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
      applemusic: 2
    };
    return platforms[platform.toLowerCase()] ?? 0;
  }
}
