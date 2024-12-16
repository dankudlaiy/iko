import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { FormsModule } from '@angular/forms';
import { MainService } from './main.service';
import { TableModule } from "primeng/table";
import { CommonModule } from "@angular/common";

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, InputTextModule, ButtonModule, FormsModule, TableModule, CommonModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css',
})
export class AppComponent {
  title = 'iko-web';
  playlistLink: string = '';

  tracks = [
    { name: 'В ритме мурр с Мэни', artist: 'DENIMANI', spotifyId: null, imageUrl: null },
    { name: "It's Beginning to Look a Lot like Christmas", artist: 'Michael Bublé', spotifyId: null, imageUrl: null },
    { name: 'Let It Snow, Let It Snow', artist: 'Dean Martin', spotifyId: null, imageUrl: null },
    { name: 'Autumn Leaves', artist: 'Louis Armstrong', spotifyId: null, imageUrl: null },
    { name: 'Autumn Leaves', artist: 'Louis Armstrong', spotifyId: null, imageUrl: null },
    { name: 'Autumn Leaves', artist: 'Louis Armstrong', spotifyId: null, imageUrl: null },
    { name: 'Autumn Leaves', artist: 'Louis Armstrong', spotifyId: null, imageUrl: null },
    { name: 'Autumn Leaves', artist: 'Louis Armstrong', spotifyId: null, imageUrl: null },
    // Add more tracks here...
  ];

  selectedTracks: any[] = [];
  showParsedInfo: boolean = false;

  constructor(private mainService: MainService) {}

  transferPlaylist() {
    console.log('playlist link', this.playlistLink);

    this.mainService.transferPlaylist(this.playlistLink).subscribe(
      (response) => {
        console.log('Playlist transferred successfully:', response);
        this.tracks = response.tracks;
      },
      (error) => {
        console.error('Error transferring playlist:', error);
      }
    );
  }

  parseSelectedTracks() {
    // Simulate parsing tracks by populating spotifyId and imageUrl
    this.selectedTracks.forEach((track) => {
      track.spotifyId = 'dummySpotifyId-' + Math.random().toString(36).substring(7); // Example Spotify ID
      track.imageUrl = 'https://via.placeholder.com/50'; // Example placeholder image
    });
    this.showParsedInfo = true;
  }
}
