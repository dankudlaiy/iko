import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class MainService {

  private apiUrl = 'https://localhost:7158/parse';
  constructor(private http: HttpClient) { }

  transferPlaylist(link: string): Observable<any> {
    return this.http.post(this.apiUrl, { link });
  }
}
