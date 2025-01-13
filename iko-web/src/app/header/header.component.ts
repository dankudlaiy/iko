import { Component } from '@angular/core';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [],
  templateUrl: './header.component.html',
  styleUrl: './header.component.css'
})
export class HeaderComponent {
  authorizeSpotify() {
    const clientId = '4fef1cbb12fa458eabfc76ebe6954d1e';
    const redirectUri = 'http://localhost:4200/callback';
    const scopes = 'playlist-modify-public playlist-modify-private';

    window.location.href = `https://accounts.spotify.com/authorize?client_id=${clientId}&response_type=code&redirect_uri=${encodeURIComponent(redirectUri)}&scope=${encodeURIComponent(scopes)}`;
  }
}
