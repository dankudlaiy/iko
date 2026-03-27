import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CdkDragDrop, DragDropModule, moveItemInArray } from '@angular/cdk/drag-drop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatMenuModule } from '@angular/material/menu';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { ApiService } from '../services/api.service';
import { PlayerService, IkoTrack } from '../services/player.service';
import { PlatformBadgeComponent } from '../platform-badge/platform-badge.component';

@Component({
  selector: 'app-playlist-editor',
  standalone: true,
  imports: [
    CommonModule, FormsModule, DragDropModule,
    MatButtonModule, MatIconModule, MatFormFieldModule, MatInputModule,
    MatSelectModule, MatSnackBarModule, MatMenuModule, PlatformBadgeComponent
  ],
  templateUrl: './playlist-editor.component.html',
  styleUrls: ['./playlist-editor.component.css']
})
export class PlaylistEditorComponent implements OnInit {
  playlistId = '';
  playlist: any = null;
  tracks: any[] = [];
  isEditingName = false;
  editName = '';

  searchQuery = '';
  searchResults: Record<string, any[]> = {};
  searchLoading = false;
  private searchSubject = new Subject<string>();

  selectedSourcePlatform = 'spotify';
  sourcePlatforms = [
    { id: 'spotify', name: 'Spotify' },
    { id: 'youtube', name: 'YouTube' },
    { id: 'applemusic', name: 'Apple Music' }
  ];
  sourcePlaylists: any[] = [];
  expandedSourcePlaylist: string | null = null;
  sourcePlaylistTracks: any[] = [];
  loadingSourcePlaylists = false;
  loadingSourceTracks = false;

  mobileTab: 'source' | 'playlist' = 'playlist';

  constructor(
    private route: ActivatedRoute,
    private api: ApiService,
    private snackBar: MatSnackBar,
    public player: PlayerService
  ) {
    this.searchSubject.pipe(
      debounceTime(400),
      distinctUntilChanged()
    ).subscribe(query => {
      if (query.trim()) {
        this.performSearch(query);
      } else {
        this.searchResults = {};
      }
    });
  }

  ngOnInit(): void {
    this.playlistId = this.route.snapshot.paramMap.get('id') || '';
    this.loadPlaylist();
    this.loadSourcePlaylists();
  }

  loadPlaylist(): void {
    this.api.getIkoPlaylist(this.playlistId).subscribe({
      next: res => {
        this.playlist = res.data;
        this.tracks = res.data?.tracks || [];
      }
    });
  }

  startEditName(): void {
    this.isEditingName = true;
    this.editName = this.playlist?.name || '';
  }

  saveName(): void {
    this.isEditingName = false;
    if (this.editName.trim() && this.editName !== this.playlist?.name) {
      this.api.updateIkoPlaylist(this.playlistId, this.editName.trim()).subscribe({
        next: () => { if (this.playlist) this.playlist.name = this.editName.trim(); }
      });
    }
  }

  onSearchInput(): void {
    this.searchSubject.next(this.searchQuery);
  }

  private performSearch(query: string): void {
    this.searchLoading = true;
    this.api.searchAllPlatforms(query).subscribe({
      next: res => {
        this.searchResults = res.data || {};
        this.searchLoading = false;
      },
      error: () => {
        this.searchResults = {};
        this.searchLoading = false;
      }
    });
  }

  searchPlatforms(): string[] {
    return Object.keys(this.searchResults).filter(k => this.searchResults[k]?.length > 0);
  }

  loadSourcePlaylists(): void {
    this.loadingSourcePlaylists = true;
    this.sourcePlaylists = [];
    this.api.getLibraryPlaylists(this.selectedSourcePlatform).subscribe({
      next: res => {
        this.sourcePlaylists = res.data || [];
        this.loadingSourcePlaylists = false;
      },
      error: () => {
        this.sourcePlaylists = [];
        this.loadingSourcePlaylists = false;
      }
    });
  }

  onSourcePlatformChange(): void {
    this.expandedSourcePlaylist = null;
    this.loadSourcePlaylists();
  }

  toggleSourcePlaylist(playlistId: string): void {
    if (this.expandedSourcePlaylist === playlistId) {
      this.expandedSourcePlaylist = null;
      return;
    }
    this.expandedSourcePlaylist = playlistId;
    this.loadingSourceTracks = true;
    this.api.getLibraryPlaylistTracks(this.selectedSourcePlatform, playlistId).subscribe({
      next: res => {
        this.sourcePlaylistTracks = res.data || [];
        this.loadingSourceTracks = false;
      },
      error: () => {
        this.sourcePlaylistTracks = [];
        this.loadingSourceTracks = false;
      }
    });
  }

  addTrack(track: any, platform?: string): void {
    const p = platform || this.selectedSourcePlatform;
    const body = {
      platform: this.api.platformIndex(p),
      platformTrackId: track.platformTrackId,
      name: track.name,
      artist: track.artist,
      imageUrl: track.imageUrl,
      durationMs: track.durationMs
    };
    this.api.addTrackToPlaylist(this.playlistId, body).subscribe({
      next: res => {
        this.tracks.push(res.data);
        this.snackBar.open('Track added', '', { duration: 2000 });
      },
      error: err => this.snackBar.open(err.error?.error || 'Failed', '', { duration: 3000 })
    });
  }

  isTrackInPlaylist(platformTrackId: string): boolean {
    return this.tracks.some(t => t.platformTrackId === platformTrackId);
  }

  removeTrack(trackId: string): void {
    this.api.removeTrackFromPlaylist(this.playlistId, trackId).subscribe({
      next: () => {
        this.tracks = this.tracks.filter(t => t.id !== trackId);
      }
    });
  }

  onDrop(event: CdkDragDrop<any[]>): void {
    moveItemInArray(this.tracks, event.previousIndex, event.currentIndex);
    const orderedIds = this.tracks.map(t => t.id);
    this.api.reorderTracks(this.playlistId, orderedIds).subscribe();
  }

  playAll(): void {
    if (this.tracks.length === 0) return;
    const queue: IkoTrack[] = this.tracks.map(t => this.toIkoTrack(t));
    this.player.playPlaylist(queue, 0);
  }

  playTrack(track: any): void {
    const queue: IkoTrack[] = this.tracks.map(t => this.toIkoTrack(t));
    const index = this.tracks.indexOf(track);
    this.player.playPlaylist(queue, index >= 0 ? index : 0);
  }

  playSourcePlaylist(): void {
    if (this.sourcePlaylistTracks.length === 0) return;
    const queue: IkoTrack[] = this.sourcePlaylistTracks.map(t => ({
      platformTrackId: t.platformTrackId,
      name: t.name,
      artist: t.artist,
      imageUrl: t.imageUrl,
      durationMs: t.durationMs,
      platform: this.mapPlatform(this.selectedSourcePlatform)
    }));
    this.player.playPlaylist(queue, 0);
  }

  exportStub(): void {
    this.snackBar.open('Export coming soon', '', { duration: 3000 });
  }

  formatDuration(ms: number): string {
    if (!ms) return '0:00';
    const s = Math.floor(ms / 1000);
    return `${Math.floor(s / 60)}:${(s % 60).toString().padStart(2, '0')}`;
  }

  platformName(index: number): string {
    return ['Spotify', 'YouTube', 'AppleMusic', 'SoundCloud', 'Deezer'][index] || '';
  }

  private toIkoTrack(t: any): IkoTrack {
    return {
      platformTrackId: t.platformTrackId,
      name: t.name,
      artist: t.artist,
      imageUrl: t.imageUrl,
      durationMs: t.durationMs,
      platform: this.mapPlatform(this.platformName(t.platform).toLowerCase())
    };
  }

  private mapPlatform(id: string): 'Spotify' | 'YouTube' | 'AppleMusic' {
    const map: Record<string, 'Spotify' | 'YouTube' | 'AppleMusic'> = {
      spotify: 'Spotify', youtube: 'YouTube', applemusic: 'AppleMusic'
    };
    return map[id] || 'Spotify';
  }
}
