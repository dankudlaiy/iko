import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router } from '@angular/router';
import { TrackCardComponent } from '../track-card/track-card.component';
import { ApiService } from '../services/api.service';
import { AuthService } from '../services/auth.service';

interface PlatformOption {
  id: string;
  name: string;
  icon: string;
  stub: boolean;
}

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [
    CommonModule, FormsModule, MatButtonModule,
    MatFormFieldModule, MatInputModule, MatProgressBarModule,
    TrackCardComponent
  ],
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.css']
})
export class HomeComponent {
  platforms: PlatformOption[] = [
    { id: 'spotify', name: 'Spotify', icon: '🎧', stub: false },
    { id: 'youtube', name: 'YouTube', icon: '▶️', stub: false },
    { id: 'applemusic', name: 'Apple Music', icon: '🍎', stub: false },
    { id: 'soundcloud', name: 'SoundCloud', icon: '☁️', stub: true },
    { id: 'deezer', name: 'Deezer', icon: '🎵', stub: true }
  ];

  fromPlatform: PlatformOption | null = null;
  toPlatform: PlatformOption | null = null;
  playlistUrl = '';

  step = 1;
  parsedTracks: any[] = [];
  matchedTracks: any[] = [];
  parseProgress = 0;
  searchProgress = 0;
  statusText = '';
  errorMessage = '';

  createdPlaylistUrl = '';
  createdPlaylistImg = '';

  constructor(
    private apiService: ApiService,
    private authService: AuthService,
    private router: Router
  ) {}

  selectFrom(platform: PlatformOption): void {
    if (platform.stub) return;
    this.fromPlatform = platform;
  }

  selectTo(platform: PlatformOption): void {
    if (platform.stub) return;
    this.toPlatform = platform;
  }

  canConvert(): boolean {
    return !!this.fromPlatform && !!this.toPlatform && !!this.playlistUrl.trim();
  }

  convert(): void {
    if (!this.authService.isLoggedIn()) {
      this.router.navigate(['/login'], { queryParams: { returnUrl: '/' } });
      return;
    }

    this.step = 2;
    this.errorMessage = '';
    this.statusText = 'Parsing playlist...';
    this.parseProgress = 0;

    this.apiService.parsePlaylist(this.fromPlatform!.id, this.playlistUrl).subscribe({
      next: (res) => {
        this.parsedTracks = res.data?.tracks || [];
        this.parseProgress = 100;
        this.statusText = `Found ${this.parsedTracks.length} tracks. Searching on ${this.toPlatform!.name}...`;
        this.searchTracks();
      },
      error: (err) => {
        this.errorMessage = err.error?.error || 'Failed to parse playlist';
        this.step = 1;
      }
    });
  }

  private searchTracks(): void {
    const tracks = this.parsedTracks.map(t => ({ name: t.name, artist: t.artist }));

    this.apiService.searchTracks(tracks, this.toPlatform!.id).subscribe({
      next: (res) => {
        const found = res.data?.tracks || [];
        this.matchedTracks = this.parsedTracks.map(t => {
          const match = found.find((m: any) => m.name === t.name && m.artist === t.artist);
          return {
            ...t,
            spotifyId: match?.spotifyId || null,
            imageUrl: match?.imageUrl || t.imageUrl || '',
            matched: !!match
          };
        });
        this.searchProgress = 100;
        const matchedCount = this.matchedTracks.filter(t => t.matched).length;
        this.statusText = `${matchedCount}/${this.parsedTracks.length} tracks matched`;
        this.step = 3;
      },
      error: (err) => {
        this.errorMessage = err.error?.error || 'Search failed';
        this.step = 1;
      }
    });
  }

  createPlaylist(): void {
    const ids = this.matchedTracks.filter(t => t.matched).map(t => t.spotifyId);
    const accessToken = localStorage.getItem('access_token');

    this.statusText = 'Creating playlist...';

    this.apiService.createExternalPlaylist(
      this.toPlatform!.id,
      ids,
      `iko - ${new Date().toLocaleDateString()}`,
      accessToken || undefined
    ).subscribe({
      next: (res: any) => {
        this.createdPlaylistUrl = res.data?.url || '';
        this.createdPlaylistImg = res.data?.img || '';
        this.step = 4;
      },
      error: (err: any) => {
        this.errorMessage = err.error?.error || 'Failed to create playlist';
      }
    });
  }

  reset(): void {
    this.step = 1;
    this.fromPlatform = null;
    this.toPlatform = null;
    this.playlistUrl = '';
    this.parsedTracks = [];
    this.matchedTracks = [];
    this.errorMessage = '';
    this.createdPlaylistUrl = '';
    this.createdPlaylistImg = '';
    this.statusText = '';
  }
}
