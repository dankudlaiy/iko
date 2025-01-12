import { Routes } from '@angular/router';
import { PlaylistInputComponent } from './playlist-input/playlist-input.component';
import { TrackListComponent } from './track-list/track-list.component';
import {SpotifyCallbackComponent} from "./spotify-callback/spotify-callback.component";

export const routes: Routes = [
  { path: '', component: PlaylistInputComponent },
  { path: 'tracks', component: TrackListComponent },
  { path: 'callback', component: SpotifyCallbackComponent }
];
