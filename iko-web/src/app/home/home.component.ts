import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideArrowRight, lucideLink, lucideExternalLink } from '@ng-icons/lucide';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmInput } from '@spartan-ng/helm/input';
import { HlmLabel } from '@spartan-ng/helm/label';
import { HlmIcon } from '@spartan-ng/helm/icon';
import { HlmProgress, HlmProgressIndicator } from '@spartan-ng/helm/progress';
import { TrackCardComponent } from '../track-card/track-card.component';
import { PlatformBadgeComponent } from '../platform-badge/platform-badge.component';
import { ApiService } from '../services/api.service';
import { AuthService } from '../services/auth.service';

interface PlatformOption {
  id: string;
  name: string;
  stub: boolean;
}

@Component({
    selector: 'app-home',
    imports: [
        FormsModule, NgIcon, HlmIcon, HlmButton, HlmInput, HlmLabel,
        HlmProgress, HlmProgressIndicator,
        TrackCardComponent, PlatformBadgeComponent
    ],
    viewProviders: [provideIcons({ lucideArrowRight, lucideLink, lucideExternalLink })],
    templateUrl: './home.component.html',
})
export class HomeComponent {
  platforms: PlatformOption[] = [
    { id: 'spotify',    name: 'Spotify',      stub: false },
    { id: 'youtube',    name: 'YouTube',      stub: false },
    { id: 'applemusic', name: 'Apple Music',  stub: false },
    { id: 'soundcloud', name: 'SoundCloud',   stub: true  },
    { id: 'deezer',     name: 'Deezer',       stub: true  }
  ];

  fromPlatform: PlatformOption | null = null;
  toPlatform: PlatformOption | null = null;
  playlistUrl = '';

  step = 1;
  parsedTracks: any[] = [];
  matchedTracks: any[] = [];
  statusText = '';
  errorMessage = '';

  createdPlaylistUrl = '';
  createdPlaylistImg = '';

  constructor(
    private apiService: ApiService,
    private authService: AuthService,
    private router: Router
  ) {}

  /** Tailwind classes for a platform tile based on its selected/stub state. */
  cardClass(selected: boolean, stub: boolean): string {
    const base =
      'relative flex flex-col items-center justify-center gap-2 rounded-xl border-2 p-3.5 text-center shadow-sm transition-all';
    if (stub) return `${base} pointer-events-none opacity-40 border-border bg-card`;
    if (selected) return `${base} border-primary bg-accent ring-2 ring-primary/30 shadow-md`;
    return `${base} border-border bg-card hover:-translate-y-0.5 hover:border-primary hover:shadow-md`;
  }

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

    this.apiService.parsePlaylist(this.fromPlatform!.id, this.playlistUrl).subscribe({
      next: (res) => {
        this.parsedTracks = res.data?.tracks || [];
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
          return { ...t, spotifyId: match?.spotifyId || null, imageUrl: match?.imageUrl || t.imageUrl || '', matched: !!match };
        });
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
      this.toPlatform!.id, ids,
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
