import { Component, Input, inject } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';

@Component({
    selector: 'app-platform-badge',
    template: `
    <span
      class="inline-flex shrink-0 items-center justify-center rounded-full"
      [style.width.px]="sizePx"
      [style.height.px]="sizePx"
      [style.background]="bgColor"
    >
      <span class="flex items-center justify-center" [innerHTML]="svgIcon"></span>
    </span>
  `,
})
export class PlatformBadgeComponent {
  @Input() platform: string = '';
  @Input() size: 'sm' | 'md' | 'lg' | 'xl' = 'md';

  private readonly sanitizer = inject(DomSanitizer);

  get sizePx(): number {
    return ({ sm: 16, md: 24, lg: 32, xl: 48 } as Record<string, number>)[this.size] || 24;
  }

  get bgColor(): string {
    const colors: Record<string, string> = {
      spotify: '#1DB954', youtube: '#FF0000', applemusic: '#FC3C44'
    };
    return colors[this.platform.toLowerCase()] || '#666';
  }

  get svgIcon(): SafeHtml {
    return this.sanitizer.bypassSecurityTrustHtml(this.rawIcon());
  }

  private rawIcon(): string {
    const s = Math.floor(this.sizePx * 0.6);
    switch (this.platform.toLowerCase()) {
      case 'spotify':
        return `<svg width="${s}" height="${s}" viewBox="0 0 24 24" fill="white"><path d="M12 0C5.4 0 0 5.4 0 12s5.4 12 12 12 12-5.4 12-12S18.66 0 12 0zm5.521 17.34c-.24.359-.66.48-1.021.24-2.82-1.74-6.36-2.101-10.561-1.141-.418.122-.779-.179-.899-.539-.12-.421.18-.78.54-.9 4.56-1.021 8.52-.6 11.64 1.32.42.18.479.659.301 1.02zm1.44-3.3c-.301.42-.841.6-1.262.3-3.239-1.98-8.159-2.58-11.939-1.38-.479.12-1.02-.12-1.14-.6-.12-.48.12-1.021.6-1.141C9.6 9.9 15 10.561 18.72 12.84c.361.181.54.78.241 1.2zm.12-3.36C15.24 8.4 8.82 8.16 5.16 9.301c-.6.179-1.2-.181-1.38-.721-.18-.601.18-1.2.72-1.381 4.26-1.26 11.28-1.02 15.721 1.621.539.3.719 1.02.419 1.56-.299.421-1.02.599-1.559.3z"/></svg>`;
      case 'youtube':
        return `<svg width="${s}" height="${s}" viewBox="0 0 24 24" fill="white"><path d="M23.498 6.186a3.016 3.016 0 0 0-2.122-2.136C19.505 3.545 12 3.545 12 3.545s-7.505 0-9.377.505A3.017 3.017 0 0 0 .502 6.186C0 8.07 0 12 0 12s0 3.93.502 5.814a3.016 3.016 0 0 0 2.122 2.136c1.871.505 9.376.505 9.376.505s7.505 0 9.377-.505a3.015 3.015 0 0 0 2.122-2.136C24 15.93 24 12 24 12s0-3.93-.502-5.814zM9.545 15.568V8.432L15.818 12l-6.273 3.568z"/></svg>`;
      case 'applemusic':
        return `<svg width="${s}" height="${s}" viewBox="0 0 24 24" fill="white"><path d="M23.994 6.124a9.23 9.23 0 0 0-.24-2.19c-.317-1.31-1.062-2.31-2.18-3.043A5.022 5.022 0 0 0 19.7.167a10.15 10.15 0 0 0-1.655-.12C17.5.01 16.96 0 14.12 0H9.87c-2.84 0-3.38.01-3.93.046a10.15 10.15 0 0 0-1.655.12 5.022 5.022 0 0 0-1.874.724C1.293 1.624.548 2.624.231 3.934a9.23 9.23 0 0 0-.24 2.19C-.046 6.67-.01 7.21 0 10.05v3.9c-.01 2.84.046 3.38.082 3.93a9.23 9.23 0 0 0 .24 2.19c.317 1.31 1.062 2.31 2.18 3.043.55.358 1.17.618 1.874.724a10.15 10.15 0 0 0 1.655.12c.546.036 1.086.046 3.926.046h4.25c2.84 0 3.38-.01 3.926-.046a10.15 10.15 0 0 0 1.655-.12 5.022 5.022 0 0 0 1.874-.724c1.118-.733 1.863-1.733 2.18-3.043a9.23 9.23 0 0 0 .24-2.19c.036-.546.046-1.086.046-3.926v-3.9c-.01-2.84-.046-3.38-.082-3.93zM16.01 11.63v5.87c0 .38-.24.71-.6.84l-5.12 1.88c-.18.06-.37.1-.56.1-.46 0-.88-.22-1.15-.56a1.5 1.5 0 0 1-.24-.84V13.6c0-.38.24-.72.6-.84l3.68-1.35V7.63c0-.38.24-.72.6-.84l.58-.21a.85.85 0 0 1 .8.11c.23.17.37.43.37.72v4.08z"/></svg>`;
      default:
        return `<svg width="${s}" height="${s}" viewBox="0 0 24 24" fill="white"><circle cx="12" cy="12" r="10"/></svg>`;
    }
  }
}
