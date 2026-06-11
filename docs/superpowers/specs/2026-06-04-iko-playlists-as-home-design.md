# Design: iko playlists as the home page (de-emphasize Convert)

Date: 2026-06-04
Status: Approved

## Context

Pivot: make **iko playlists (the library)** the product's main surface and home page, and remove the **Convert** flow from navigation. Convert is kept (code + a hidden route) for later — not deleted. Library requires auth, so a logged-out visitor at `/` is redirected to Login (no marketing/landing exists yet).

## Routing (`src/app/app.routes.ts`)

```
{ path: '',                    component: LibraryComponent,        canActivate: [authGuard] }  // home = library
{ path: 'login',               component: LoginComponent }
{ path: 'register',            component: RegisterComponent }
{ path: 'convert',             component: HomeComponent }          // kept, not in nav
{ path: 'library',             redirectTo: '', pathMatch: 'full' } // alias for existing /library links
{ path: 'library/playlist/:id',component: PlaylistEditorComponent, canActivate: [authGuard] }
{ path: 'settings',            component: SettingsComponent,       canActivate: [authGuard] }
{ path: 'callback/:platform',  component: CallbackComponent }
{ path: '**',                  redirectTo: '' }
```

Guest at `/` → `authGuard` → `/login?returnUrl=/` → after login returns to library. Existing post-login/register navigations to `/library` still resolve (alias → `''`).

## Header (`src/app/header/header.component.html`)

- Remove the **Convert** link from the desktop nav and the mobile drawer.
- **Library** nav item → `routerLink="/"` with `[routerLinkActiveOptions]="{ exact: true }"`. Brand logo already → `/`.
- Logged-in: `Library · Settings · email · Logout · theme`. Guest: `Login · Sign Up · theme`.

## Unchanged

Library content/layout, playlist editor, cover (mosaic/custom) and volume features, `HomeComponent` (now reachable only at `/convert`).

## Out of scope

Real playlist export (currently a stub in the editor), any public landing/marketing page.

## Verification

Frontend prod build green. Logged-in: `/` shows the library; "Convert" absent from nav; Library active-highlights at `/`. Guest: `/` redirects to `/login`; old `/library` link still resolves to the library.
