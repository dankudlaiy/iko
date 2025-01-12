import {Component, OnInit} from '@angular/core';
import {ActivatedRoute, Router} from '@angular/router';
import {MainService} from '../../../services/main.service';
import {FormsModule} from "@angular/forms";
import {InputTextModule} from "primeng/inputtext";
import {ButtonModule} from "primeng/button";
import {TableModule} from "primeng/table";
import {CommonModule, NgOptimizedImage} from '@angular/common';
import {bufferCount, concatMap, delay, mergeMap, of} from 'rxjs';

@Component({
  selector: 'app-track-list',
  templateUrl: './track-list.component.html',
  standalone: true,
  imports: [FormsModule, InputTextModule, ButtonModule, TableModule, CommonModule, NgOptimizedImage],
  styleUrls: ['./track-list.component.css', '../../styles.css']
})
export class TrackListComponent implements OnInit {
  tracks: any[] = [];
  playlistLink: string = '';
  isLoading: boolean = false;
  errorMessage: string = '';
  createdPlaylist: string = '';

  constructor(private route: ActivatedRoute, private mainService: MainService, private router: Router) {}

  ngOnInit(): void {
    debugger;

    this.route.queryParams.subscribe(params => {
      this.playlistLink = params['link'];

      if (this.playlistLink) {
        this.parse();
      }
    });
  }

  parse() {
    this.isLoading = true;
    this.errorMessage = '';

    this.mainService.parsePlaylist(this.playlistLink).subscribe(
      response => {
        this.tracks = response.tracks;
        this.fetchTrackDetails();
        this.isLoading = false;
      },
      error => {
        this.isLoading = false;
        this.errorMessage = 'Error transferring playlist. Please try again.';
        console.error('Error transferring playlist:', error);
      }
    );
  }

  fetchTrackDetails() {
    of(...this.tracks)
      .pipe(
        bufferCount(10),
        concatMap((trackGroup, groupIndex) =>
          of(...trackGroup).pipe(
            mergeMap((track) =>
              this.mainService.searchForSpotifyTrack(track.name, track.artist)
            ),
            delay(1500)
          )
        )
      )
      .subscribe(response => {
        const index = this.tracks.findIndex(t => t.name === response.name && t.artist === response.artist);
        if (index !== -1) {
          this.tracks[index].spotifyId = response.spotifyId;
          this.tracks[index].imageUrl = response.imageUrl;
        }
      });
  }

  removeTrack(index: number) {
    this.tracks.splice(index, 1);
  }

  createPlaylist() {
    const accessToken = localStorage.getItem('access_token');
    console.log('retrieved Access Token:', accessToken);

    if (!accessToken) {
      console.error('Access token is missing.');
      this.errorMessage = 'Access token is missing. Please authenticate again.';
      return;
    }

    const ids = this.tracks.map(track => track.spotifyId);
    this.mainService.createPlaylist(ids, accessToken).subscribe(
      response => {
        console.log(response);
        this.createdPlaylist = response.url;
      }
    );
  }

  goBack() {
    this.router.navigate(['']);
  }

  authorizeSpotify() {
    const clientId = '4fef1cbb12fa458eabfc76ebe6954d1e';
    const redirectUri = 'http://localhost:4200/callback';
    const scopes = 'playlist-modify-public playlist-modify-private';

    window.location.href = `https://accounts.spotify.com/authorize?client_id=${clientId}&response_type=code&redirect_uri=${encodeURIComponent(redirectUri)}&scope=${encodeURIComponent(scopes)}`;
  }
}