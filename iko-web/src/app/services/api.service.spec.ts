import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { ApiService } from './api.service';
import { environment } from '../../environments/environment';

describe('ApiService', () => {
  let service: ApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(ApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('maps platform names to backend enum indexes', () => {
    expect(service.platformIndex('spotify')).toBe(0);
    expect(service.platformIndex('YouTube')).toBe(1);
    expect(service.platformIndex('applemusic')).toBe(2);
    expect(service.platformIndex('unknown')).toBe(0);
  });

  it('requests iko playlists', () => {
    service.getIkoPlaylists().subscribe();

    const req = http.expectOne(`${environment.apiUrl}/iko-playlists`);
    expect(req.request.method).toBe('GET');
    req.flush({ data: [], error: null });
  });

  it('exports a playlist with the platform index in the body', () => {
    service.exportIkoPlaylist('pl-1', 'youtube').subscribe();

    const req = http.expectOne(`${environment.apiUrl}/iko-playlists/pl-1/export`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ targetPlatform: 1 });
    req.flush({ data: null, error: null });
  });

  it('adds a track to a playlist', () => {
    const body = {
      platform: 0, platformTrackId: 't-1', name: 'Song',
      artist: 'Artist', imageUrl: null, durationMs: 1000
    };
    service.addTrackToPlaylist('pl-1', body).subscribe();

    const req = http.expectOne(`${environment.apiUrl}/iko-playlists/pl-1/tracks`);
    expect(req.request.body).toEqual(body);
    req.flush({ data: null, error: null });
  });
});
