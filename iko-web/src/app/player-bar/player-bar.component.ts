import { Component } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideShuffle, lucideSkipBack, lucideSkipForward,
  lucidePlay, lucidePause, lucideRepeat, lucideRepeat1,
  lucideVolume2, lucideVolume1, lucideVolumeX
} from '@ng-icons/lucide';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmIcon } from '@spartan-ng/helm/icon';
import { PlayerService } from '../services/player.service';
import { PlatformBadgeComponent } from '../platform-badge/platform-badge.component';

@Component({
    selector: 'app-player-bar',
    imports: [NgIcon, HlmIcon, HlmButton, PlatformBadgeComponent],
    viewProviders: [provideIcons({
      lucideShuffle, lucideSkipBack, lucideSkipForward,
      lucidePlay, lucidePause, lucideRepeat, lucideRepeat1,
      lucideVolume2, lucideVolume1, lucideVolumeX
    })],
    templateUrl: './player-bar.component.html',
})
export class PlayerBarComponent {
  constructor(public player: PlayerService) {}

  get track() { return this.player.currentTrack; }
  get s() { return this.player.state; }

  onSeek(event: any): void {
    this.player.seekTo(event.target.value);
  }

  onVolume(event: any): void {
    this.player.setVolume(Number(event.target.value));
  }

  get volumeIcon(): string {
    if (this.s.isMuted || this.s.volume === 0) return 'lucideVolumeX';
    if (this.s.volume < 0.5) return 'lucideVolume1';
    return 'lucideVolume2';
  }

  togglePlay(): void {
    if (this.s.isPlaying) {
      this.player.pause();
    } else {
      this.player.resume();
    }
  }
}
