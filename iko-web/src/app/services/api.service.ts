import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  AccessTokenResult,
  AddTrackBody,
  ApiResponse,
  AppleConfig,
  ConnectedAccount,
  ConnectUrlResult,
  ExportResult,
  IkoPlaylistDetail,
  IkoPlaylistSummary,
  IkoPlaylistTrack,
  LibraryPlaylist,
  LibraryTrack,
  SearchResults
} from '../models';

const API_URL = environment.apiUrl;

@Injectable({ providedIn: 'root' })
export class ApiService {
  constructor(private http: HttpClient) {}

  // Connected accounts
  getConnectedAccounts(): Observable<ApiResponse<ConnectedAccount[]>> {
    return this.http.get<ApiResponse<ConnectedAccount[]>>(`${API_URL}/accounts`);
  }

  disconnectAccount(platform: string): Observable<ApiResponse<boolean>> {
    return this.http.delete<ApiResponse<boolean>>(`${API_URL}/accounts/${this.platformIndex(platform)}`);
  }

  getConnectUrl(platform: string): Observable<ApiResponse<ConnectUrlResult>> {
    return this.http.get<ApiResponse<ConnectUrlResult>>(`${API_URL}/accounts/connect/${platform.toLowerCase()}`);
  }

  getAccountToken(platform: string): Observable<ApiResponse<AccessTokenResult>> {
    return this.http.get<ApiResponse<AccessTokenResult>>(`${API_URL}/accounts/token/${this.platformIndex(platform)}`);
  }

  // Iko playlists
  getIkoPlaylists(): Observable<ApiResponse<IkoPlaylistSummary[]>> {
    return this.http.get<ApiResponse<IkoPlaylistSummary[]>>(`${API_URL}/iko-playlists`);
  }

  createIkoPlaylist(name: string): Observable<ApiResponse<IkoPlaylistSummary>> {
    return this.http.post<ApiResponse<IkoPlaylistSummary>>(`${API_URL}/iko-playlists`, { name });
  }

  getIkoPlaylist(id: string): Observable<ApiResponse<IkoPlaylistDetail>> {
    return this.http.get<ApiResponse<IkoPlaylistDetail>>(`${API_URL}/iko-playlists/${id}`);
  }

  updateIkoPlaylist(id: string, name: string): Observable<ApiResponse<Pick<IkoPlaylistDetail, 'id' | 'name'>>> {
    return this.http.put<ApiResponse<Pick<IkoPlaylistDetail, 'id' | 'name'>>>(`${API_URL}/iko-playlists/${id}`, { name });
  }

  deleteIkoPlaylist(id: string): Observable<ApiResponse<boolean>> {
    return this.http.delete<ApiResponse<boolean>>(`${API_URL}/iko-playlists/${id}`);
  }

  addTrackToPlaylist(playlistId: string, track: AddTrackBody): Observable<ApiResponse<IkoPlaylistTrack>> {
    return this.http.post<ApiResponse<IkoPlaylistTrack>>(`${API_URL}/iko-playlists/${playlistId}/tracks`, track);
  }

  removeTrackFromPlaylist(playlistId: string, trackId: string): Observable<ApiResponse<boolean>> {
    return this.http.delete<ApiResponse<boolean>>(`${API_URL}/iko-playlists/${playlistId}/tracks/${trackId}`);
  }

  reorderTracks(playlistId: string, orderedIds: string[]): Observable<ApiResponse<boolean>> {
    return this.http.patch<ApiResponse<boolean>>(`${API_URL}/iko-playlists/${playlistId}/tracks/reorder`, { orderedIds });
  }

  uploadPlaylistCover(playlistId: string, file: File): Observable<ApiResponse<Pick<IkoPlaylistDetail, 'id' | 'name' | 'coverUrl'>>> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<ApiResponse<Pick<IkoPlaylistDetail, 'id' | 'name' | 'coverUrl'>>>(
      `${API_URL}/iko-playlists/${playlistId}/cover`, form);
  }

  removePlaylistCover(playlistId: string): Observable<ApiResponse<Pick<IkoPlaylistDetail, 'id' | 'name' | 'coverUrl'>>> {
    return this.http.delete<ApiResponse<Pick<IkoPlaylistDetail, 'id' | 'name' | 'coverUrl'>>>(
      `${API_URL}/iko-playlists/${playlistId}/cover`);
  }

  exportIkoPlaylist(playlistId: string, platform: string): Observable<ApiResponse<ExportResult>> {
    return this.http.post<ApiResponse<ExportResult>>(
      `${API_URL}/iko-playlists/${playlistId}/export`,
      { targetPlatform: this.platformIndex(platform) }
    );
  }

  // Library
  getLibraryPlaylists(platform: string): Observable<ApiResponse<LibraryPlaylist[]>> {
    return this.http.get<ApiResponse<LibraryPlaylist[]>>(
      `${API_URL}/library/playlists/${this.platformIndex(platform)}`);
  }

  getLibraryPlaylistTracks(platform: string, playlistId: string): Observable<ApiResponse<LibraryTrack[]>> {
    return this.http.get<ApiResponse<LibraryTrack[]>>(
      `${API_URL}/library/playlists/${this.platformIndex(platform)}/${playlistId}/tracks`);
  }

  // Search
  searchAllPlatforms(query: string, platforms = 'Spotify,YouTube'): Observable<ApiResponse<SearchResults>> {
    return this.http.get<ApiResponse<SearchResults>>(`${API_URL}/search`, { params: { q: query, platforms } });
  }

  // Apple config
  getAppleConfig(): Observable<ApiResponse<AppleConfig>> {
    return this.http.get<ApiResponse<AppleConfig>>(`${API_URL}/config/apple`);
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
