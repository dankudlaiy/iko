import { Injectable } from '@angular/core';
import { toast } from 'ngx-sonner';
import { ApiService } from './api.service';

export interface IkoTrack {
  platformTrackId: string;
  name: string;
  artist: string;
  imageUrl?: string | null;
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
  volume: number;
  isMuted: boolean;
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
    loadingMessage: '',
    volume: 1,
    isMuted: false
  };

  // Third-party player SDKs (Spotify Web Playback, YouTube IFrame, MusicKit)
  // ship no TypeScript typings, so their instances stay `any` deliberately.
  private spotifyPlayer: any = null;
  private spotifyDeviceId: string | null = null;
  private ytPlayer: any = null;
  private musicKitInstance: any = null;
  private positionInterval: ReturnType<typeof setInterval> | null = null;
  private _prewarming = false;

  constructor(private api: ApiService) {
    const storedVolume = parseFloat(localStorage.getItem('iko_volume') ?? '1');
    if (!isNaN(storedVolume)) this.state.volume = Math.min(1, Math.max(0, storedVolume));
    this.state.isMuted = localStorage.getItem('iko_muted') === 'true';
    this.prewarm();
  }

  /**
   * Eagerly create the Spotify player so that activateElement() can run within
   * the very first user gesture (required to unlock audio on mobile browsers).
   * Best-effort: silently no-ops if logged out / Spotify not connected.
   */
  async prewarm(): Promise<void> {
    if (this.spotifyPlayer || this._prewarming) return;
    if (!localStorage.getItem('iko_token')) return;
    this._prewarming = true;
    try {
      await this.initSpotifySdk();
    } catch {
      /* not connected / no Premium — real error surfaces on actual play */
    } finally {
      this._prewarming = false;
    }
  }

  /**
   * Unlocks mobile audio. MUST be called synchronously from a user gesture
   * (tap on play/next/prev) before any await, or mobile browsers block playback.
   */
  activateForGesture(): void {
    try { this.spotifyPlayer?.activateElement?.(); } catch { /* ignore */ }
  }

  private setYtVisible(visible: boolean): void {
    const c = document.getElementById('yt-player-container');
    if (c) c.style.display = visible ? 'block' : 'none';
  }

  setVolume(v: number): void {
    this.state.volume = Math.min(1, Math.max(0, v));
    if (this.state.volume > 0) this.state.isMuted = false;
    localStorage.setItem('iko_volume', String(this.state.volume));
    localStorage.setItem('iko_muted', String(this.state.isMuted));
    this.applyVolume();
  }

  toggleMute(): void {
    this.state.isMuted = !this.state.isMuted;
    localStorage.setItem('iko_muted', String(this.state.isMuted));
    this.applyVolume();
  }

  private applyVolume(): void {
    const v = this.state.isMuted ? 0 : this.state.volume;
    try {
      switch (this.state.currentPlatform) {
        case 'Spotify': this.spotifyPlayer?.setVolume(v); break;
        case 'YouTube': this.ytPlayer?.setVolume?.(Math.round(v * 100)); break;
        case 'AppleMusic': if (this.musicKitInstance) this.musicKitInstance.volume = v; break;
      }
    } catch { /* SDK not ready yet — volume re-applied on next track load */ }
  }

  get currentTrack(): IkoTrack | null {
    if (this.state.currentIndex < 0 || this.state.currentIndex >= this.state.queue.length) return null;
    return this.state.queue[this.state.currentIndex];
  }

  playPlaylist(tracks: IkoTrack[], startIndex = 0): void {
    this.activateForGesture();
    this.state.queue = [...tracks];
    this.state.currentIndex = startIndex;
    this.playCurrentTrack();
  }

  playTrack(track: IkoTrack): void {
    this.activateForGesture();
    this.state.queue = [track];
    this.state.currentIndex = 0;
    this.playCurrentTrack();
  }

  async pause(): Promise<void> {
    this.state.isPlaying = false;
    await this.pauseCurrentSdk();
  }

  async resume(): Promise<void> {
    this.activateForGesture();
    this.state.isPlaying = true;
    await this.resumeCurrentSdk();
  }

  async next(): Promise<void> {
    this.activateForGesture();
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
    this.activateForGesture();
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
    if (track.platform !== 'YouTube') this.setYtVisible(false);

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
      this.applyVolume();
    } catch (err: any) {
      toast(err.message || 'Playback failed');
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

    const tokenRes = await this.api.getAccountToken('spotify').toPromise();
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
    const tokenRes = await this.api.getAccountToken('spotify').toPromise();
    const token = tokenRes?.data?.accessToken;
    if (!token) throw new Error('Spotify Premium required for playback');

    return new Promise((resolve, reject) => {
      this.spotifyPlayer = new (window as any).Spotify.Player({
        name: 'iko',
        getOAuthToken: (cb: (t: string) => void) => {
          this.api.getAccountToken('spotify').subscribe({
            next: res => cb(res.data?.accessToken || ''),
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
        toast('Spotify playback requires Premium. Upgrade at spotify.com');
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
    this.setYtVisible(true);
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
      // Rendered off-screen: the iframe must stay a real, non-hidden element for audio
      // to keep playing (display:none stops it), but we push it out of view so only the
      // audio is used. NOTE: mobile browsers may refuse to play an off-screen video.
      container.style.cssText =
        'position:fixed;width:200px;height:120px;left:-10000px;top:0;pointer-events:none;display:none;';
      document.body.appendChild(container);
    }

    const playerDiv = document.createElement('div');
    playerDiv.id = 'yt-player';
    container.appendChild(playerDiv);

    this.ytPlayer = new (window as any).YT.Player('yt-player', {
      height: '90',
      width: '160',
      playerVars: { playsinline: 1, rel: 0 },
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

    const configRes = await this.api.getAppleConfig().toPromise();
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
      toast('Apple Music playback requires an Apple Music subscription');
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
