import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class MainService {

  private apiUrl = 'https://localhost:7158';
  constructor(private http: HttpClient) { }

  parsePlaylist(link: string): Observable<any> {
    return this.http.post(`${this.apiUrl}/parse`, { link });
  }

  searchForSpotifyTrack(name: string, artist: string): Observable<any> {
    return this.http.get(`${this.apiUrl}/search?name=${encodeURIComponent(name)}&artist=${encodeURIComponent(artist)}`);
  }

  obtainAccessToken(authToken: string): Observable<any> {
    return this.http.get(`${this.apiUrl}/auth?token=${encodeURIComponent(authToken)}`);
  }

  createPlaylist(ids: any[], token: string): Observable<any> {
    return this.http.post(`${this.apiUrl}/create`, { ids, token });
  }
}
