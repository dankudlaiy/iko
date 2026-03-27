import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
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
  standalone: true,
  imports: [CommonModule, MatCardModule, MatButtonModule, MatSnackBarModule],
  templateUrl: './settings.component.html',
  styleUrls: ['./settings.component.css']
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
    private playerService: PlayerService,
    private snackBar: MatSnackBar
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
          alert(res.message);
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
          this.snackBar.open(`Disconnected ${platformName} — playback stopped`, '', { duration: 5000 });
        }
      }
    });
  }

  logout(): void {
    this.authService.logout();
  }
}
