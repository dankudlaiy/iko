import { PlayerService, IkoTrack } from './player.service';

/**
 * Regression tests for the auto-advance guard.
 *
 * Player SDKs (notably Spotify's Web Playback SDK) emit several "track ended"
 * events per track end. Before the guard, each one called next() again, which
 * overlapped playback (two tracks at once) and left the position poller pointing
 * at a stale SDK (frozen progress bar). next() now holds an `isAdvancing` lock
 * across the whole advance so duplicate end signals collapse into a single one.
 *
 * playCurrentTrack() is stubbed so these tests exercise only the advance logic
 * (no real SDK / network).
 */
describe('PlayerService auto-advance guard', () => {
  let svc: PlayerService;
  const apiStub = {} as any;

  beforeEach(() => {
    localStorage.clear();
    svc = new PlayerService(apiStub);
  });

  function seedQueue(n: number): void {
    svc.state.queue = Array.from({ length: n }, (_, i): IkoTrack => ({
      platformTrackId: 't' + i, name: 'T' + i, artist: 'A', durationMs: 1000, platform: 'YouTube'
    }));
    svc.state.currentIndex = 0;
  }

  it('advances only once when duplicate end signals fire for the same track', async () => {
    seedQueue(3);
    const play = spyOn<any>(svc, 'playCurrentTrack').and.returnValue(Promise.resolve());

    // Two SDK "ended" events arriving back-to-back for the same track.
    (svc as any).handleTrackEnded();
    (svc as any).handleTrackEnded();

    expect(svc.state.currentIndex).toBe(1); // advanced once, not to 2
    expect(play).toHaveBeenCalledTimes(1);

    await Promise.resolve(); // let next()'s await settle so the lock releases

    // A later, legitimate track end advances again.
    (svc as any).handleTrackEnded();
    expect(svc.state.currentIndex).toBe(2);
  });

  it('stops at the end of the queue when repeat is off', async () => {
    seedQueue(2);
    spyOn<any>(svc, 'playCurrentTrack').and.returnValue(Promise.resolve());
    svc.state.currentIndex = 1;
    svc.state.isPlaying = true;

    await svc.next();

    expect(svc.state.currentIndex).toBe(1);
    expect(svc.state.isPlaying).toBe(false);
  });

  it('wraps to the start at end of queue when repeat=all', async () => {
    seedQueue(2);
    spyOn<any>(svc, 'playCurrentTrack').and.returnValue(Promise.resolve());
    svc.state.currentIndex = 1;
    svc.state.repeatMode = 'all';

    await svc.next();

    expect(svc.state.currentIndex).toBe(0);
  });
});
