import { environment } from '../../environments/environment';

// API base ends with `/api`; uploaded media is served from the host root
// (e.g. `/uploads/covers/...`). Strip the `/api` suffix to get the media origin.
// In prod `apiUrl` is `/api` → origin is '' → same-origin relative paths.
const MEDIA_ORIGIN = environment.apiUrl.replace(/\/api\/?$/, '');

/** Resolve a stored media path/URL to a loadable URL. Absolute URLs pass through. */
export function mediaUrl(path: string | null | undefined): string {
  if (!path) return '';
  if (/^https?:\/\//i.test(path)) return path;
  return `${MEDIA_ORIGIN}${path}`;
}
