// Development environment (used via fileReplacements for `ng serve`).
// `apiUrl` is relative so it works on localhost, LAN, and tunnels alike;
// the dev server proxies `/api` and `/uploads` to the backend (see proxy.conf.json).
export const environment = {
  production: false,
  apiUrl: '/api',
};
