import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatSliderModule } from '@angular/material/slider';
import { PlayerService } from '../services/player.service';
import { PlatformBadgeComponent } from '../platform-badge/platform-badge.component';

@Component({
  selector: 'app-player-bar',
  standalone: true,
  imports: [CommonModule, MatIconModule, MatButtonModule, MatSliderModule, PlatformBadgeComponent],
  templateUrl: './player-bar.component.html',
  styleUrls: ['./player-bar.component.css']
})
export class PlayerBarComponent {
  constructor(public player: PlayerService) {}

  get track() { return this.player.currentTrack; }
  get s() { return this.player.state; }

  onSeek(event: any): void {
    this.player.seekTo(event.target.value);
  }

  togglePlay(): void {
    if (this.s.isPlaying) {
      this.player.pause();
    } else {
      this.player.resume();
    }
  }
}
