import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideMenu, lucideX, lucideSun, lucideMoon } from '@ng-icons/lucide';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmIcon } from '@spartan-ng/helm/icon';
import { AuthService, UserInfo } from '../services/auth.service';
import { ThemeService } from '../services/theme.service';

@Component({
    selector: 'app-header',
    imports: [RouterLink, RouterLinkActive, NgIcon, HlmIcon, HlmButton],
    viewProviders: [provideIcons({ lucideMenu, lucideX, lucideSun, lucideMoon })],
    templateUrl: './header.component.html',
})
export class HeaderComponent {
  user: UserInfo | null = null;
  mobileMenuOpen = false;
  readonly theme = inject(ThemeService);

  constructor(private authService: AuthService) {
    this.authService.currentUser$.subscribe(u => this.user = u);
  }

  get isLoggedIn(): boolean {
    return this.authService.isLoggedIn();
  }

  logout(): void {
    this.authService.logout();
    this.mobileMenuOpen = false;
  }

  toggleMenu(): void {
    this.mobileMenuOpen = !this.mobileMenuOpen;
  }

  closeMenu(): void {
    this.mobileMenuOpen = false;
  }
}
