import { Component, OnInit } from '@angular/core';
import { toast } from 'ngx-sonner';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmBadge } from '@spartan-ng/helm/badge';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { AuthService, UserInfo } from '../services/auth.service';
import { ApiService } from '../services/api.service';
import { PlayerService } from '../services/player.service';

interface PlatformConfig {
  id: string;
  name: string;
  icon: string;
  stub: boolean;
}

@Component({
    selector: 'app-settings',
    imports: [...HlmCardImports, HlmButton, HlmBadge],
    templateUrl: './settings.component.html',
})
export class SettingsComponent implements OnInit {
  user: UserInfo | null = null;
  connectedAccounts: any[] = [];

  platforms: PlatformConfig[] = [
    { id: 'spotify', name: 'Spotify', icon: '🎧', stub: false },
    { id: 'youtube', name: 'YouTube', icon: '▶️', stub: false },
    { id: 'applemusic', name: 'Apple Music', icon: '🍎', stub: false },
    { id: 'soundcloud', name: 'SoundCloud', icon: '☁️', stub: true },
    { id: 'deezer', name: 'Deezer', icon: '🎵', stub: true }
  ];

  constructor(
    private authService: AuthService,
    private apiService: ApiService,
    private playerService: PlayerService
  ) {}

  ngOnInit(): void {
    this.authService.currentUser$.subscribe(user => this.user = user);
    this.loadAccounts();
  }

  loadAccounts(): void {
    this.apiService.getConnectedAccounts().subscribe({
      next: res => this.connectedAccounts = res.data || [],
      error: () => this.connectedAccounts = []
    });
  }

  isConnected(platformId: string): boolean {
    const platformMap: Record<string, number> = {
      spotify: 0, youtube: 1, applemusic: 2, soundcloud: 3, deezer: 4
    };
    return this.connectedAccounts.some(a => a.platform === platformMap[platformId]);
  }

  getDisplayName(platformId: string): string {
    const platformMap: Record<string, number> = {
      spotify: 0, youtube: 1, applemusic: 2, soundcloud: 3, deezer: 4
    };
    const account = this.connectedAccounts.find(a => a.platform === platformMap[platformId]);
    return account?.platformDisplayName || account?.platformUserId || '';
  }

  connect(platform: PlatformConfig): void {
    if (platform.stub) return;

    this.apiService.getConnectUrl(platform.id).subscribe({
      next: res => {
        if (res.data?.url) {
          window.location.href = res.data.url;
        } else if (res.message) {
          toast(res.message);
        }
      }
    });
  }

  disconnect(platformId: string): void {
    const platformMap: Record<string, string> = {
      spotify: 'Spotify', youtube: 'YouTube', applemusic: 'AppleMusic'
    };
    const platformName = platformMap[platformId];

    this.apiService.disconnectAccount(platformId).subscribe({
      next: () => {
        this.loadAccounts();
        if (platformName && this.playerService.state.currentPlatform === platformName) {
          this.playerService.pause();
          toast(`Disconnected ${platformName} — playback stopped`);
        }
      }
    });
  }

  logout(): void {
    this.authService.logout();
  }
}
