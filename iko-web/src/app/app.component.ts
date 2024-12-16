import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { FormsModule } from '@angular/forms';
import { MainService } from './main.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, InputTextModule, ButtonModule, FormsModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent {
  title = 'iko-web';
  playlistLink: string = '';

  constructor(private mainService: MainService) {}

  transferPlaylist() {
    console.log('playlist link', this.playlistLink);

    this.mainService.transferPlaylist(this.playlistLink).subscribe(
      (response) => {
        console.log('Playlist transferred successfully:', response);
      },
      (error) => {
        console.error('Error transferring playlist:', error);
      }
    );
  }
}
