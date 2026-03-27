import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTabsModule } from '@angular/material/tabs';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatMenuModule } from '@angular/material/menu';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../services/api.service';
import { PlayerService, IkoTrack } from '../services/player.service';
import { PlatformBadgeComponent } from '../platform-badge/platform-badge.component';

@Component({
  selector: 'app-library',
  standalone: true,
  imports: [
    CommonModule, MatButtonModule, MatIconModule, MatTabsModule,
    MatDialogModule, MatFormFieldModule, MatInputModule, MatTooltipModule,
    MatSnackBarModule, FormsModule, PlatformBadgeComponent,
    MatMenuModule
  ],
  templateUrl: './library.component.html',
  styleUrls: ['./library.component.css']
})
export class LibraryComponent implements OnInit {
  ikoPlaylists: any[] = [];
  platformTabs = [
    { id: 'spotify', name: 'Spotify', connected: false },
    { id: 'youtube', name: 'YouTube', connected: false },
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

  constructor(
    private api: ApiService,
    private router: Router,
    private snackBar: MatSnackBar,
    public player: PlayerService
  ) {}

  ngOnInit(): void {
    this.loadIkoPlaylists();
    this.loadConnectedAccounts();
  }

  loadIkoPlaylists(): void {
    this.api.getIkoPlaylists().subscribe({
      next: res => this.ikoPlaylists = res.data || [],
      error: () => this.ikoPlaylists = []
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
      next: () => this.snackBar.open('Track added', '', { duration: 2000 }),
      error: err => this.snackBar.open(err.error?.error || 'Failed to add track', '', { duration: 3000 })
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
      error: () => this.snackBar.open('Failed to create playlist', '', { duration: 3000 })
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
