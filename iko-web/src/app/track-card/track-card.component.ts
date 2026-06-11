import { Component, Input } from '@angular/core';
import { HlmBadge } from '@spartan-ng/helm/badge';

@Component({
    selector: 'app-track-card',
    imports: [HlmBadge],
    templateUrl: './track-card.component.html',
})
export class TrackCardComponent {
  @Input() name = '';
  @Input() artist = '';
  @Input() imageUrl = '';
  @Input() matched = false;
}
