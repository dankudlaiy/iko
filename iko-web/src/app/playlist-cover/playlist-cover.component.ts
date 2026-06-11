import { Component, Input } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideListMusic } from '@ng-icons/lucide';
import { HlmIcon } from '@spartan-ng/helm/icon';
import { mediaUrl } from '../services/media.util';

/**
 * Renders an iko-playlist cover following the precedence rule:
 *   custom `coverUrl` → 2x2 mosaic of 4+ track images → single image → placeholder.
 */
@Component({
  selector: 'app-playlist-cover',
  imports: [NgIcon, HlmIcon],
  viewProviders: [provideIcons({ lucideListMusic })],
  template: `
    @if (resolvedCover) {
      <img [src]="resolvedCover" alt="" class="size-full object-cover" />
    } @else if (showMosaic) {
      <div class="grid size-full grid-cols-2 grid-rows-2">
        @for (img of tiles; track $index) {
          <img [src]="img" alt="" class="size-full object-cover" />
        }
      </div>
    } @else if (images.length) {
      <img [src]="images[0]" alt="" class="size-full object-cover" />
    } @else {
      <div class="flex size-full items-center justify-center bg-muted text-muted-foreground">
        <ng-icon hlm name="lucideListMusic" class="text-3xl" />
      </div>
    }
  `,
})
export class PlaylistCoverComponent {
  @Input() coverUrl: string | null = null;
  @Input() images: string[] = [];

  get resolvedCover(): string {
    return mediaUrl(this.coverUrl);
  }

  get showMosaic(): boolean {
    return this.images.length >= 4;
  }

  get tiles(): string[] {
    return this.images.slice(0, 4);
  }
}
