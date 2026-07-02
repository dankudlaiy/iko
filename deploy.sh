#!/usr/bin/env bash
#
# Deploy iko to the production server (AWS Lightsail) over the `awqserver` SSH alias.
#
# Native deploy: builds locally, ships artifacts, restarts the systemd service.
#   - Backend  -> /var/www/iko/api  (published .NET app, run by `iko-api` systemd unit)
#   - Frontend -> /var/www/iko/web  (static Angular build, served by nginx)
#
# Does NOT touch:
#   - the server .env (holds Jwt__Key, App__* and rotated platform secrets)
#   - the API directory's runtime data (iko.db, logs/, wwwroot/uploads/)
#
# Prerequisites: dotnet SDK 8, Node/npm, ssh access as `awqserver`.
# Usage: ./deploy.sh
set -euo pipefail

SERVER=awqserver
API_DIR=/var/www/iko/api
WEB_DIR=/var/www/iko/web
PUBLISH_DIR=deploy-publish
WEB_DIST=iko-web/dist/iko-web/browser
SITE=https://13.219.111.35.sslip.io

cd "$(dirname "$0")"

echo "==> Building backend (Release)..."
rm -rf "$PUBLISH_DIR"
dotnet publish iko-host/iko-host.csproj -c Release -o "$PUBLISH_DIR" --nologo

echo "==> Building frontend (production)..."
if [ ! -d iko-web/node_modules ]; then
  (cd iko-web && npm ci)
fi
(cd iko-web && npx ng build --configuration production)

echo "==> Uploading backend (overwrite app files, keep db/logs/uploads)..."
scp -q -r "$PUBLISH_DIR"/. "$SERVER:$API_DIR/"

echo "==> Uploading frontend (replace static files)..."
ssh "$SERVER" "rm -rf ${WEB_DIR:?}/*"
scp -q -r "$WEB_DIST"/. "$SERVER:$WEB_DIR/"

echo "==> Restarting API service..."
ssh "$SERVER" "sudo systemctl restart iko-api && sleep 3 && systemctl is-active iko-api"

echo "==> Smoke test..."
curl -s -o /dev/null -w "  frontend: %{http_code}\n" "$SITE/"
curl -s -o /dev/null -w "  api (401 expected): %{http_code}\n" "$SITE/api/library/playlists/0"

echo "==> Deploy complete: $SITE"
