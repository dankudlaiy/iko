import { DOCUMENT, Injectable, effect, inject, signal } from '@angular/core';

export type Theme = 'light' | 'dark' | 'system';

const STORAGE_KEY = 'iko_theme';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly doc = inject(DOCUMENT);
  private readonly mql = this.doc.defaultView!.matchMedia('(prefers-color-scheme: dark)');

  /** The user's selected preference: explicit light/dark, or follow the system. */
  readonly theme = signal<Theme>(this.read());

  constructor() {
    // Re-apply when the OS theme changes while in "system" mode.
    this.mql.addEventListener('change', () => {
      if (this.theme() === 'system') this.apply();
    });
    // Apply whenever the preference changes.
    effect(() => {
      this.theme();
      this.apply();
    });
  }

  /** Whether dark mode is currently active (after resolving "system"). */
  get isDark(): boolean {
    const t = this.theme();
    return t === 'dark' || (t === 'system' && this.mql.matches);
  }

  setTheme(theme: Theme): void {
    localStorage.setItem(STORAGE_KEY, theme);
    this.theme.set(theme);
  }

  /** Flip between light and dark (collapses "system" to an explicit choice). */
  toggle(): void {
    this.setTheme(this.isDark ? 'light' : 'dark');
  }

  private read(): Theme {
    const stored = localStorage.getItem(STORAGE_KEY) as Theme | null;
    return stored === 'light' || stored === 'dark' || stored === 'system' ? stored : 'system';
  }

  private apply(): void {
    this.doc.documentElement.classList.toggle('dark', this.isDark);
  }
}
