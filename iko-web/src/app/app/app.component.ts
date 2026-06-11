import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { trigger, transition, style, animate, query } from '@angular/animations';
import { HlmToaster } from '@spartan-ng/helm/sonner';
import { HeaderComponent } from '../header/header.component';
import { PlayerBarComponent } from '../player-bar/player-bar.component';
import { PlayerService } from '../services/player.service';
import { ThemeService } from '../services/theme.service';

@Component({
    selector: 'app-root',
    imports: [RouterOutlet, HeaderComponent, PlayerBarComponent, HlmToaster],
    templateUrl: './app.component.html',
    animations: [
        trigger('routeAnim', [
            transition('* <=> *', [
                query(':enter', [
                    style({ opacity: 0, transform: 'translateY(10px)' }),
                    animate('250ms cubic-bezier(0.35, 0, 0.25, 1)', style({ opacity: 1, transform: 'translateY(0)' }))
                ], { optional: true })
            ])
        ])
    ]
})
export class AppComponent {
  readonly theme = inject(ThemeService);

  constructor(public playerService: PlayerService) {}

  getRoute(outlet: RouterOutlet): string {
    return outlet.isActivated
      ? (outlet.activatedRoute?.snapshot?.url?.[0]?.path || 'home')
      : '';
  }
}
