import { Component } from '@angular/core';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';

@Component({
  selector: 'app-playlist-input',
  templateUrl: './playlist-input.component.html',
  standalone: true,
  imports: [FormsModule, InputTextModule, ButtonModule],
  styleUrls: ['./playlist-input.component.css', '../../styles.css']
})
export class PlaylistInputComponent {
  playlistLink: string = '';

  constructor(private router: Router) {}

  navigateToTracks() {
    this.router.navigate(['/tracks'], { queryParams: { link: this.playlistLink } });
  }
}
