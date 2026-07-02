import { Component, Input } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideMusic } from '@ng-icons/lucide';
import { HlmIcon } from '@spartan-ng/helm/icon';
import { HlmBadge } from '@spartan-ng/helm/badge';

@Component({
    selector: 'app-track-card',
    imports: [HlmBadge, NgIcon, HlmIcon],
    viewProviders: [provideIcons({ lucideMusic })],
    templateUrl: './track-card.component.html',
})
export class TrackCardComponent {
  @Input() name = '';
  @Input() artist = '';
  @Input() imageUrl = '';
  @Input() matched = false;
}
