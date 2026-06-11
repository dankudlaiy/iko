import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { trigger, transition, style, animate, stagger, query } from '@angular/animations';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucidePlus, lucideListMusic, lucidePlay, lucideLock } from '@ng-icons/lucide';
import { toast } from 'ngx-sonner';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmInput } from '@spartan-ng/helm/input';
import { HlmLabel } from '@spartan-ng/helm/label';
import { HlmIcon } from '@spartan-ng/helm/icon';
import { HlmSkeleton } from '@spartan-ng/helm/skeleton';
import { HlmTooltip } from '@spartan-ng/helm/tooltip';
import { HlmDropdownMenuImports } from '@spartan-ng/helm/dropdown-menu';
import { ApiService } from '../services/api.service';
import { PlayerService, IkoTrack } from '../services/player.service';
import { PlatformBadgeComponent } from '../platform-badge/platform-badge.component';
import { PlaylistCoverComponent } from '../playlist-cover/playlist-cover.component';

@Component({
    selector: 'app-library',
    imports: [
        FormsModule, NgIcon, HlmIcon, HlmButton, HlmInput, HlmLabel,
        HlmSkeleton, HlmTooltip, ...HlmDropdownMenuImports, PlatformBadgeComponent, PlaylistCoverComponent
    ],
    viewProviders: [provideIcons({ lucidePlus, lucideListMusic, lucidePlay, lucideLock })],
    templateUrl: './library.component.html',
    animations: [
        trigger('cardStagger', [
            transition('* => *', [
                query(':enter', [
                    style({ opacity: 0, transform: 'scale(0.92) translateY(8px)' }),
                    stagger(45, animate('220ms ease', style({ opacity: 1, transform: 'scale(1) translateY(0)' })))
                ], { optional: true })
            ])
        ])
    ]
})
export class LibraryComponent implements OnInit {
  ikoPlaylists: any[] = [];
  loadingIkoPlaylists = true;

  platformTabs = [
    { id: 'spotify',    name: 'Spotify',     connected: false },
    { id: 'youtube',    name: 'YouTube',     connected: false },
    { id: 'applemusic', name: 'Apple Music', connected: false }
  ];
  selectedPlatformTab = 'spotify';
  platformPlaylists: any[] = [];
  expandedPlaylistId: string | null = null;
  expandedPlaylistTracks: any[] = [];
  connectedAccounts: any[] = [];
  loadingPlatform = false;
  loadingTracks = false;

  newPlaylistName = '';
  showNewPlaylistDialog = false;

  readonly skeletonCards = Array(8).fill(0);
  readonly skeletonTracks = Array(6).fill(0);

  constructor(
    private api: ApiService,
    private router: Router,
    public player: PlayerService
  ) {}

  ngOnInit(): void {
    this.loadIkoPlaylists();
    this.loadConnectedAccounts();
  }

  loadIkoPlaylists(): void {
    this.loadingIkoPlaylists = true;
    this.api.getIkoPlaylists().subscribe({
      next: res => {
        this.ikoPlaylists = res.data || [];
        this.loadingIkoPlaylists = false;
      },
      error: () => {
        this.ikoPlaylists = [];
        this.loadingIkoPlaylists = false;
      }
    });
  }

  loadConnectedAccounts(): void {
    this.api.getConnectedAccounts().subscribe({
      next: res => {
        this.connectedAccounts = res.data || [];
        this.platformTabs.forEach(tab => {
          const platformIndex = this.api.platformIndex(tab.id);
          tab.connected = this.connectedAccounts.some(a => a.platform === platformIndex);
        });
        const firstConnected = this.platformTabs.find(t => t.connected);
        if (firstConnected) {
          this.selectedPlatformTab = firstConnected.id;
          this.loadPlatformPlaylists(firstConnected.id);
        }
      }
    });
  }

  selectPlatformTab(tabId: string): void {
    const tab = this.platformTabs.find(t => t.id === tabId);
    if (!tab?.connected) return;
    this.selectedPlatformTab = tabId;
    this.expandedPlaylistId = null;
    this.loadPlatformPlaylists(tabId);
  }

  loadPlatformPlaylists(platform: string): void {
    this.loadingPlatform = true;
    this.platformPlaylists = [];
    this.api.getLibraryPlaylists(platform).subscribe({
      next: res => {
        this.platformPlaylists = res.data || [];
        this.loadingPlatform = false;
      },
      error: () => {
        this.platformPlaylists = [];
        this.loadingPlatform = false;
      }
    });
  }

  togglePlaylistExpand(playlistId: string): void {
    if (this.expandedPlaylistId === playlistId) {
      this.expandedPlaylistId = null;
      return;
    }
    this.expandedPlaylistId = playlistId;
    this.loadingTracks = true;
    this.api.getLibraryPlaylistTracks(this.selectedPlatformTab, playlistId).subscribe({
      next: res => {
        this.expandedPlaylistTracks = res.data || [];
        this.loadingTracks = false;
      },
      error: () => {
        this.expandedPlaylistTracks = [];
        this.loadingTracks = false;
      }
    });
  }

  addToIkoPlaylist(track: any, ikoPlaylistId: string): void {
    const body = {
      platform: this.api.platformIndex(this.selectedPlatformTab),
      platformTrackId: track.platformTrackId,
      name: track.name,
      artist: track.artist,
      imageUrl: track.imageUrl,
      durationMs: track.durationMs
    };
    this.api.addTrackToPlaylist(ikoPlaylistId, body).subscribe({
      next: () => toast('Track added'),
      error: err => toast(err.error?.error || 'Failed to add track')
    });
  }

  openIkoPlaylist(id: string): void {
    this.router.navigate(['/library/playlist', id]);
  }

  createNewPlaylist(): void {
    if (!this.newPlaylistName.trim()) return;
    this.api.createIkoPlaylist(this.newPlaylistName.trim()).subscribe({
      next: res => {
        this.showNewPlaylistDialog = false;
        this.newPlaylistName = '';
        this.router.navigate(['/library/playlist', res.data.id]);
      },
      error: () => toast('Failed to create playlist')
    });
  }

  playPlatformPlaylist(playlist: any): void {
    if (this.expandedPlaylistId !== playlist.id) {
      this.togglePlaylistExpand(playlist.id);
    }
    setTimeout(() => {
      if (this.expandedPlaylistTracks.length > 0) {
        const tracks: IkoTrack[] = this.expandedPlaylistTracks.map((t: any) => ({
          platformTrackId: t.platformTrackId,
          name: t.name,
          artist: t.artist,
          imageUrl: t.imageUrl,
          durationMs: t.durationMs,
          platform: this.mapPlatform(this.selectedPlatformTab)
        }));
        this.player.playPlaylist(tracks, 0);
      }
    }, 1500);
  }

  private mapPlatform(id: string): 'Spotify' | 'YouTube' | 'AppleMusic' {
    const map: Record<string, 'Spotify' | 'YouTube' | 'AppleMusic'> = {
      spotify: 'Spotify', youtube: 'YouTube', applemusic: 'AppleMusic'
    };
    return map[id] || 'Spotify';
  }
}
