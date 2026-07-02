# iko — Deployment & Ops

Everything needed to deploy, operate, and continue working on the production server
from any machine. No secret values live in this file — only structure and locations.

## Production at a glance

| | |
|---|---|
| Host | AWS Lightsail, Ubuntu 24.04, x86_64 (1.9 GB RAM + 2 GB swap) |
| Public URL | https://13.219.111.35.sslip.io |
| Static IP | `13.219.111.35` (attached in Lightsail; `*.sslip.io` resolves to it automatically) |
| Firewall (Lightsail → Networking) | 22 (SSH), 80 (HTTP), 443 (HTTPS) open |
| Backend | ASP.NET Core 8 (Kestrel) on `127.0.0.1:5000`, `systemd` unit `iko-api` |
| Frontend | Angular static build served by nginx |
| Reverse proxy | nginx: static at `/`, proxy `/api` and `/uploads` → Kestrel |
| TLS | Let's Encrypt (certbot, auto-renew) |
| Database | SQLite at `/var/www/iko/api/iko.db`, EF Core migrations run on startup |

## Server layout

```
/var/www/iko/api/     published .NET app (iko-host.dll), .env, iko.db, logs/, wwwroot/uploads/
/var/www/iko/web/     static Angular build (index.html + assets)
/etc/systemd/system/iko-api.service
/etc/nginx/sites-available/iko  (symlinked into sites-enabled)
```

Another app, `celesteagent.com`, also lives on this box (its own nginx site); iko does
not touch it.

## Access from a new PC

1. **Get the SSH key.** It's the Lightsail default key for the `us-east-1` region.
   Download it: Lightsail console → the instance → Connect tab → **Download default key**
   (the `.pem`). The key is intentionally **not** in git.
2. **Install the key** and lock it down (Git Bash / WSL / macOS):
   ```bash
   cp /path/to/LightsailDefaultKey-us-east-1.pem ~/.ssh/awqserver.pem
   chmod 600 ~/.ssh/awqserver.pem
   ```
3. **Add the SSH host** to `~/.ssh/config`:
   ```
   Host awqserver
       HostName 13.219.111.35
       User ubuntu
       IdentityFile ~/.ssh/awqserver.pem
       IdentitiesOnly yes
   ```
4. **Verify:** `ssh awqserver 'echo ok'`.

## Deploying

From the repo root, one command builds both projects, uploads, restarts, smoke-tests:

```bash
./deploy.sh
```

Requires: .NET SDK 8, Node/npm, and the `awqserver` SSH host above. The script:
- publishes `iko-host` (Release) → uploads to `/var/www/iko/api` (overwrites app files,
  **keeps** `iko.db`, `logs/`, `wwwroot/uploads/`),
- builds `iko-web` (production) → replaces `/var/www/iko/web`,
- `sudo systemctl restart iko-api`, then curls the site.

It never touches the server `.env`.

## Secrets / configuration (server `.env`)

Location: `/var/www/iko/api/.env` (chmod 600, **not** in git). Keys:

```
SPOTIFY_CLIENT_ID / SPOTIFY_CLIENT_SECRET
YOUTUBE_CLIENT_ID / YOUTUBE_CLIENT_SECRET / YOUTUBE_API_KEY
APPLE_DEVELOPER_TOKEN            # optional; Apple Music is disabled in the UI
Jwt__Key                        # random, generated on the server
App__ApiBaseUrl                 # https://13.219.111.35.sslip.io  (OAuth callback origin)
App__WebBaseUrl                 # https://13.219.111.35.sslip.io  (SPA origin, post-login redirect, CORS)
```

- `appsettings.json` holds only a **development** JWT placeholder and localhost
  `App:*` defaults; the server `.env` overrides them (DotNetEnv loads `.env`, and
  `__` maps to `:` in config).
- The file uses **LF** line endings. When copying from Windows, strip CR:
  `sed -i 's/\r$//' /var/www/iko/api/.env` — otherwise values get a trailing `\r`
  and OAuth/token calls fail with `invalid_client`.
- To change a secret: edit the value on the server (or re-upload) and
  `sudo systemctl restart iko-api`.

### OAuth provider setup (Spotify / Google)

Registered redirect URIs must match `App__ApiBaseUrl` exactly:
- Spotify (Dashboard → app → Redirect URIs):
  `https://13.219.111.35.sslip.io/api/accounts/callback/spotify`
- Google (Cloud Console → OAuth client → Authorized redirect URIs):
  `https://13.219.111.35.sslip.io/api/accounts/callback/youtube`
- Spotify scopes include `playlist-read-private playlist-read-collaborative` (needed to
  list private/collaborative playlists) — set in `AccountsController.ConnectSpotify`.

## Common operations

```bash
# status / logs
ssh awqserver 'systemctl status iko-api --no-pager'
ssh awqserver 'journalctl -u iko-api -n 100 --no-pager'   # app logs (also /var/www/iko/api/logs/)

# restart after a config/.env change
ssh awqserver 'sudo systemctl restart iko-api'

# nginx
ssh awqserver 'sudo nginx -t && sudo systemctl reload nginx'
cat /etc/nginx/sites-available/iko   # (on server)

# TLS (auto-renews; to check / force)
ssh awqserver 'sudo certbot certificates'
ssh awqserver 'sudo certbot renew --dry-run'

# database (SQLite)
ssh awqserver 'sqlite3 /var/www/iko/api/iko.db ".tables"'
```

## Continue from another PC — checklist

1. `git clone https://github.com/dankudlaiy/iko.git`
2. Install the SSH key + config (see "Access from a new PC").
3. For local dev: copy `.env.example` → `iko-host/.env`, fill in credentials
   (see [../README.md](../README.md)).
4. Backend: `cd iko-host && dotnet run`; frontend: `cd iko-web && npm install && npm start`.
5. To ship: `./deploy.sh`.

## Notes / gotchas

- **1.9 GB RAM**: 2 GB swap is configured; heavy builds run locally (deploy uploads
  artifacts), not on the server.
- **sslip.io depends on the IP**: if the Lightsail static IP ever changes, the domain
  and the TLS cert must be re-issued for the new `<ip>.sslip.io`.
- **Apple Music** is removed from the UI; the client code remains for data integrity of
  any previously-saved Apple tracks.
