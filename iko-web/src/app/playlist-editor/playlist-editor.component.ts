import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CdkDragDrop, DragDropModule, moveItemInArray } from '@angular/cdk/drag-drop';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideSearch, lucideChevronRight, lucidePlay, lucidePencil,
  lucideX, lucideGripVertical, lucideListMusic, lucidePlus, lucideCheck, lucideCamera
} from '@ng-icons/lucide';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { toast } from 'ngx-sonner';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmInput } from '@spartan-ng/helm/input';
import { HlmIcon } from '@spartan-ng/helm/icon';
import { HlmSkeleton } from '@spartan-ng/helm/skeleton';
import { HlmDropdownMenuImports } from '@spartan-ng/helm/dropdown-menu';
import { ApiService } from '../services/api.service';
import { PlayerService, IkoTrack } from '../services/player.service';
import { ExportResult, IkoPlaylistDetail, IkoPlaylistTrack, LibraryPlaylist, LibraryTrack, SearchResults, SearchTrack } from '../models';
import { PlatformBadgeComponent } from '../platform-badge/platform-badge.component';
import { PlaylistCoverComponent } from '../playlist-cover/playlist-cover.component';

@Component({
    selector: 'app-playlist-editor',
    imports: [
        FormsModule, DragDropModule, NgIcon, HlmIcon, HlmButton, HlmInput, HlmSkeleton,
        ...HlmDropdownMenuImports, PlatformBadgeComponent, PlaylistCoverComponent
    ],
    viewProviders: [provideIcons({
      lucideSearch, lucideChevronRight, lucidePlay, lucidePencil,
      lucideX, lucideGripVertical, lucideListMusic, lucidePlus, lucideCheck, lucideCamera
    })],
    templateUrl: './playlist-editor.component.html',
})
export class PlaylistEditorComponent implements OnInit {
  playlistId = '';
  playlist: IkoPlaylistDetail | null = null;
  tracks: IkoPlaylistTrack[] = [];
  isEditingName = false;
  editName = '';

  searchQuery = '';
  searchResults: SearchResults = {};
  searchLoading = false;
  private searchSubject = new Subject<string>();

  selectedSourcePlatform = 'spotify';
  sourcePlatforms = [
    { id: 'spotify', name: 'Spotify' },
    { id: 'youtube', name: 'YouTube' },
    { id: 'applemusic', name: 'Apple Music' }
  ];
  sourcePlaylists: LibraryPlaylist[] = [];
  expandedSourcePlaylist: string | null = null;
  sourcePlaylistTracks: LibraryTrack[] = [];
  loadingSourcePlaylists = false;
  loadingSourceTracks = false;

  mobileTab: 'source' | 'playlist' = 'playlist';

  constructor(
    private route: ActivatedRoute,
    private api: ApiService,
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
        this.tracks = res.data?.tracks ?? [];
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

  get trackImages(): string[] {
    return this.tracks.map(t => t.imageUrl).filter((u): u is string => !!u);
  }

  onCoverSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    this.api.uploadPlaylistCover(this.playlistId, file).subscribe({
      next: res => {
        if (this.playlist) this.playlist.coverUrl = res.data?.coverUrl ?? null;
        toast('Cover updated');
      },
      error: err => toast(err.error?.error || 'Failed to upload cover')
    });
    input.value = '';
  }

  removeCover(): void {
    this.api.removePlaylistCover(this.playlistId).subscribe({
      next: () => {
        if (this.playlist) this.playlist.coverUrl = null;
        toast('Cover removed');
      },
      error: () => toast('Failed to remove cover')
    });
  }

  onSearchInput(): void {
    this.searchSubject.next(this.searchQuery);
  }

  private performSearch(query: string): void {
    this.searchLoading = true;
    this.api.searchAllPlatforms(query).subscribe({
      next: res => {
        this.searchResults = res.data ?? {};
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
        this.sourcePlaylists = res.data ?? [];
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
        this.sourcePlaylistTracks = res.data ?? [];
        this.loadingSourceTracks = false;
      },
      error: () => {
        this.sourcePlaylistTracks = [];
        this.loadingSourceTracks = false;
      }
    });
  }

  addTrack(track: SearchTrack | LibraryTrack, platform?: string): void {
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
        if (res.data) this.tracks.push(res.data);
        toast('Track added');
      },
      error: err => toast(err.error?.error || 'Failed')
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

  onDrop(event: CdkDragDrop<IkoPlaylistTrack[]>): void {
    moveItemInArray(this.tracks, event.previousIndex, event.currentIndex);
    const orderedIds = this.tracks.map(t => t.id);
    this.api.reorderTracks(this.playlistId, orderedIds).subscribe();
  }

  playAll(): void {
    if (this.tracks.length === 0) return;
    const queue: IkoTrack[] = this.tracks.map(t => this.toIkoTrack(t));
    this.player.playPlaylist(queue, 0);
  }

  playTrack(track: IkoPlaylistTrack): void {
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

  exporting = false;
  exportResult: ExportResult | null = null;

  exportTo(platform: string, platformLabel: string): void {
    if (this.exporting) return;
    this.exporting = true;
    this.exportResult = null;
    // getAccountToken refreshes a stale access token server-side before exporting
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

  formatDuration(ms: number): string {
    if (!ms) return '0:00';
    const s = Math.floor(ms / 1000);
    return `${Math.floor(s / 60)}:${(s % 60).toString().padStart(2, '0')}`;
  }

  platformName(index: number): string {
    return ['Spotify', 'YouTube', 'AppleMusic'][index] || '';
  }

  private toIkoTrack(t: IkoPlaylistTrack): IkoTrack {
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
