import { Injectable } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ApiService } from './api.service';

export interface IkoTrack {
  platformTrackId: string;
  name: string;
  artist: string;
  imageUrl?: string;
  durationMs: number;
  platform: 'Spotify' | 'YouTube' | 'AppleMusic';
}

export interface PlayerState {
  queue: IkoTrack[];
  currentIndex: number;
  isPlaying: boolean;
  currentPlatform: 'Spotify' | 'YouTube' | 'AppleMusic' | null;
  positionMs: number;
  durationMs: number;
  isShuffle: boolean;
  repeatMode: 'none' | 'one' | 'all';
  isLoading: boolean;
  loadingMessage: string;
}

@Injectable({ providedIn: 'root' })
export class PlayerService {
  state: PlayerState = {
    queue: [],
    currentIndex: -1,
    isPlaying: false,
    currentPlatform: null,
    positionMs: 0,
    durationMs: 0,
    isShuffle: false,
    repeatMode: 'none',
    isLoading: false,
    loadingMessage: ''
  };

  private spotifyPlayer: any = null;
  private spotifyDeviceId: string | null = null;
  private ytPlayer: any = null;
  private musicKitInstance: any = null;
  private positionInterval: any = null;

  constructor(private snackBar: MatSnackBar, private api: ApiService) {}

  get currentTrack(): IkoTrack | null {
    if (this.state.currentIndex < 0 || this.state.currentIndex >= this.state.queue.length) return null;
    return this.state.queue[this.state.currentIndex];
  }

  playPlaylist(tracks: IkoTrack[], startIndex = 0): void {
    this.state.queue = [...tracks];
    this.state.currentIndex = startIndex;
    this.playCurrentTrack();
  }

  playTrack(track: IkoTrack): void {
    this.state.queue = [track];
    this.state.currentIndex = 0;
    this.playCurrentTrack();
  }

  async pause(): Promise<void> {
    this.state.isPlaying = false;
    await this.pauseCurrentSdk();
  }

  async resume(): Promise<void> {
    this.state.isPlaying = true;
    await this.resumeCurrentSdk();
  }

  async next(): Promise<void> {
    if (this.state.repeatMode === 'one') {
      this.playCurrentTrack();
      return;
    }

    let nextIndex = this.state.currentIndex + 1;

    if (this.state.isShuffle) {
      nextIndex = Math.floor(Math.random() * this.state.queue.length);
    }

    if (nextIndex >= this.state.queue.length) {
      if (this.state.repeatMode === 'all') {
        nextIndex = 0;
      } else {
        this.state.isPlaying = false;
        return;
      }
    }

    this.state.currentIndex = nextIndex;
    this.playCurrentTrack();
  }

  async previous(): Promise<void> {
    if (this.state.positionMs > 3000) {
      this.seekTo(0);
      return;
    }

    let prevIndex = this.state.currentIndex - 1;
    if (prevIndex < 0) prevIndex = this.state.queue.length - 1;
    this.state.currentIndex = prevIndex;
    this.playCurrentTrack();
  }

  async seekTo(positionMs: number): Promise<void> {
    this.state.positionMs = positionMs;
    const track = this.currentTrack;
    if (!track) return;

    switch (track.platform) {
      case 'Spotify':
        if (this.spotifyPlayer) await this.spotifyPlayer.seek(positionMs);
        break;
      case 'YouTube':
        if (this.ytPlayer) this.ytPlayer.seekTo(positionMs / 1000, true);
        break;
      case 'AppleMusic':
        if (this.musicKitInstance) await this.musicKitInstance.seekToTime(positionMs / 1000);
        break;
    }
  }

  toggleShuffle(): void {
    this.state.isShuffle = !this.state.isShuffle;
  }

  toggleRepeat(): void {
    const modes: ('none' | 'one' | 'all')[] = ['none', 'all', 'one'];
    const idx = modes.indexOf(this.state.repeatMode);
    this.state.repeatMode = modes[(idx + 1) % modes.length];
  }

  formatTime(ms: number): string {
    const totalSeconds = Math.floor(ms / 1000);
    const m = Math.floor(totalSeconds / 60);
    const s = totalSeconds % 60;
    return `${m}:${s.toString().padStart(2, '0')}`;
  }

  private async playCurrentTrack(): Promise<void> {
    const track = this.currentTrack;
    if (!track) return;

    if (this.state.currentPlatform && this.state.currentPlatform !== track.platform) {
      await this.pauseCurrentSdk();
      this.state.isLoading = true;
      this.state.loadingMessage = `Switching to ${track.platform}...`;
    }

    this.state.currentPlatform = track.platform;
    this.state.positionMs = 0;
    this.state.durationMs = track.durationMs || 0;

    try {
      switch (track.platform) {
        case 'Spotify':
          await this.playSpotify(track);
          break;
        case 'YouTube':
          await this.playYouTube(track);
          break;
        case 'AppleMusic':
          await this.playAppleMusic(track);
          break;
      }
      this.state.isPlaying = true;
    } catch (err: any) {
      this.snackBar.open(err.message || 'Playback failed', 'OK', { duration: 5000 });
      this.state.isPlaying = false;
    }

    this.state.isLoading = false;
    this.state.loadingMessage = '';
  }

  // --- Spotify ---
  private async playSpotify(track: IkoTrack): Promise<void> {
    if (!this.spotifyPlayer) {
      await this.initSpotifySdk();
    }

    const tokenRes: any = await this.api.getAccountToken('spotify').toPromise();
    const token = tokenRes?.data?.accessToken;
    if (!token) throw new Error('Spotify not connected');

    await fetch(`https://api.spotify.com/v1/me/player/play?device_id=${this.spotifyDeviceId}`, {
      method: 'PUT',
      headers: { 'Authorization': `Bearer ${token}`, 'Content-Type': 'application/json' },
      body: JSON.stringify({ uris: [`spotify:track:${track.platformTrackId}`] })
    });

    this.startPositionPolling('spotify');
  }

  private initSpotifySdk(): Promise<void> {
    return new Promise((resolve, reject) => {
      if ((window as any).Spotify) {
        this.createSpotifyPlayer().then(resolve).catch(reject);
        return;
      }

      (window as any).onSpotifyWebPlaybackSDKReady = () => {
        this.createSpotifyPlayer().then(resolve).catch(reject);
      };

      const script = document.createElement('script');
      script.src = 'https://sdk.scdn.co/spotify-player.js';
      document.head.appendChild(script);
    });
  }

  private async createSpotifyPlayer(): Promise<void> {
    const tokenRes: any = await this.api.getAccountToken('spotify').toPromise();
    const token = tokenRes?.data?.accessToken;
    if (!token) throw new Error('Spotify Premium required for playback');

    return new Promise((resolve, reject) => {
      this.spotifyPlayer = new (window as any).Spotify.Player({
        name: 'iko',
        getOAuthToken: (cb: (t: string) => void) => {
          this.api.getAccountToken('spotify').subscribe({
            next: (res: any) => cb(res?.data?.accessToken || ''),
            error: () => cb('')
          });
        },
        volume: 0.5
      });

      this.spotifyPlayer.addListener('ready', ({ device_id }: any) => {
        this.spotifyDeviceId = device_id;
        resolve();
      });

      this.spotifyPlayer.addListener('not_ready', () => {});

      this.spotifyPlayer.addListener('player_state_changed', (s: any) => {
        if (!s) return;
        this.state.positionMs = s.position;
        this.state.durationMs = s.duration;
        if (s.paused && s.position === 0 && s.track_window?.previous_tracks?.length > 0) {
          this.next();
        }
      });

      this.spotifyPlayer.addListener('initialization_error', ({ message }: any) => {
        this.snackBar.open('Spotify playback requires Premium. Upgrade at spotify.com', 'OK', { duration: 7000 });
        reject(new Error(message));
      });

      this.spotifyPlayer.connect();
    });
  }

  // --- YouTube ---
  private async playYouTube(track: IkoTrack): Promise<void> {
    if (!this.ytPlayer) {
      await this.initYouTubeSdk();
    }

    this.ytPlayer.loadVideoById(track.platformTrackId);
    this.startPositionPolling('youtube');
  }

  private initYouTubeSdk(): Promise<void> {
    return new Promise((resolve) => {
      if ((window as any).YT && (window as any).YT.Player) {
        this.createYouTubePlayer(resolve);
        return;
      }

      (window as any).onYouTubeIframeAPIReady = () => {
        this.createYouTubePlayer(resolve);
      };

      const script = document.createElement('script');
      script.src = 'https://www.youtube.com/iframe_api';
      document.head.appendChild(script);
    });
  }

  private createYouTubePlayer(resolve: () => void): void {
    let container = document.getElementById('yt-player-container');
    if (!container) {
      container = document.createElement('div');
      container.id = 'yt-player-container';
      container.style.cssText = 'position:fixed;width:1px;height:1px;bottom:0;right:0;overflow:hidden;';
      document.body.appendChild(container);
    }

    const playerDiv = document.createElement('div');
    playerDiv.id = 'yt-player';
    container.appendChild(playerDiv);

    this.ytPlayer = new (window as any).YT.Player('yt-player', {
      height: '1',
      width: '1',
      events: {
        onReady: () => resolve(),
        onStateChange: (event: any) => {
          if (event.data === (window as any).YT.PlayerState.ENDED) {
            this.next();
          }
          if (event.data === (window as any).YT.PlayerState.PLAYING) {
            this.state.durationMs = (this.ytPlayer.getDuration() || 0) * 1000;
          }
        }
      }
    });
  }

  // --- Apple Music ---
  private async playAppleMusic(track: IkoTrack): Promise<void> {
    if (!this.musicKitInstance) {
      await this.initAppleMusicSdk();
    }

    await this.musicKitInstance.setQueue({ song: track.platformTrackId });
    await this.musicKitInstance.play();
    this.startPositionPolling('applemusic');
  }

  private async initAppleMusicSdk(): Promise<void> {
    if (!(window as any).MusicKit) {
      await new Promise<void>((resolve) => {
        const script = document.createElement('script');
        script.src = 'https://js-cdn.music.apple.com/musickit/v3/musickit.js';
        script.onload = () => resolve();
        document.head.appendChild(script);
      });

      await new Promise(resolve => setTimeout(resolve, 500));
    }

    const configRes: any = await this.api.getAppleConfig().toPromise();
    const developerToken = configRes?.data?.developerToken;
    if (!developerToken) throw new Error('Apple Music not configured');

    await (window as any).MusicKit.configure({
      developerToken,
      app: { name: 'iko', build: '1.0' }
    });

    this.musicKitInstance = (window as any).MusicKit.getInstance();

    try {
      await this.musicKitInstance.authorize();
    } catch {
      this.snackBar.open('Apple Music playback requires an Apple Music subscription', 'OK', { duration: 7000 });
      throw new Error('Apple Music authorization failed');
    }

    this.musicKitInstance.addEventListener('playbackStateDidChange', () => {
      if (this.musicKitInstance.playbackState === 10) {
        this.next();
      }
    });
  }

  // --- Position polling ---
  private startPositionPolling(platform: string): void {
    this.stopPositionPolling();
    this.positionInterval = setInterval(() => {
      if (!this.state.isPlaying) return;

      switch (platform) {
        case 'spotify':
          if (this.spotifyPlayer) {
            this.spotifyPlayer.getCurrentState().then((s: any) => {
              if (s) {
                this.state.positionMs = s.position;
                this.state.durationMs = s.duration;
              }
            });
          }
          break;
        case 'youtube':
          if (this.ytPlayer && this.ytPlayer.getCurrentTime) {
            this.state.positionMs = this.ytPlayer.getCurrentTime() * 1000;
          }
          break;
        case 'applemusic':
          if (this.musicKitInstance) {
            this.state.positionMs = (this.musicKitInstance.currentPlaybackTime || 0) * 1000;
            this.state.durationMs = (this.musicKitInstance.currentPlaybackDuration || 0) * 1000;
          }
          break;
      }
    }, 500);
  }

  private stopPositionPolling(): void {
    if (this.positionInterval) {
      clearInterval(this.positionInterval);
      this.positionInterval = null;
    }
  }

  private async pauseCurrentSdk(): Promise<void> {
    this.stopPositionPolling();
    switch (this.state.currentPlatform) {
      case 'Spotify':
        if (this.spotifyPlayer) await this.spotifyPlayer.pause();
        break;
      case 'YouTube':
        if (this.ytPlayer) this.ytPlayer.pauseVideo();
        break;
      case 'AppleMusic':
        if (this.musicKitInstance) await this.musicKitInstance.pause();
        break;
    }
  }

  private async resumeCurrentSdk(): Promise<void> {
    switch (this.state.currentPlatform) {
      case 'Spotify':
        if (this.spotifyPlayer) await this.spotifyPlayer.resume();
        break;
      case 'YouTube':
        if (this.ytPlayer) this.ytPlayer.playVideo();
        break;
      case 'AppleMusic':
        if (this.musicKitInstance) await this.musicKitInstance.play();
        break;
    }
    this.startPositionPolling(this.state.currentPlatform?.toLowerCase() || '');
  }
}
