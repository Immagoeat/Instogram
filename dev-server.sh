#!/usr/bin/env bash
# Start Instogram server locally + expose it via ngrok
# Usage: ./dev-server.sh
# Optional env vars:
#   PORT=5000               override the server port
#   JWT_KEY=...             use a specific JWT key (generated once and saved to .env)
#   MASTER_PASSWORD=...     create an admin account on first run
#   MASTER_USERNAME=...     admin username (default: admin)
#   MASTER_DISPLAY_NAME=... admin display name (default: Admin)

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ENV_FILE="$SCRIPT_DIR/.env"

# Load persisted config from .env if present
if [ -f "$ENV_FILE" ]; then
    # shellcheck disable=SC1090
    set -a; source "$ENV_FILE"; set +a
fi

PORT="${PORT:-5001}"

# Generate and persist JWT key once so tokens survive restarts
if [ -z "$JWT_KEY" ]; then
    JWT_KEY="$(openssl rand -base64 48)"
    echo "JWT_KEY=$JWT_KEY" >> "$ENV_FILE"
    echo "[setup] Generated and saved JWT_KEY to .env"
fi

# Kill background jobs on exit
cleanup() {
    kill "$SERVER_PID" "$NGROK_PID" 2>/dev/null || true
}
trap cleanup EXIT INT TERM

echo "=== Instogram Dev Server ==="
echo ""

# Start the server
cd "$SCRIPT_DIR/InstogramServer"
ASPNETCORE_URLS="http://localhost:$PORT" \
Jwt__Key="$JWT_KEY" \
MasterPassword="${MASTER_PASSWORD:-}" \
MasterUsername="${MASTER_USERNAME:-admin}" \
MasterDisplayName="${MASTER_DISPLAY_NAME:-Admin}" \
    dotnet run --no-launch-profile &
SERVER_PID=$!

# Wait for server to be ready
echo "Waiting for server on port $PORT…"
for i in $(seq 1 30); do
    if curl -s -o /dev/null -w "%{http_code}" "http://localhost:$PORT/" 2>/dev/null | grep -qE "^[0-9]"; then
        break
    fi
    sleep 1
done

# Start ngrok
echo "Starting ngrok tunnel…"
ngrok http "$PORT" --log=stdout --log-format=json > /tmp/ngrok-instogram.log 2>&1 &
NGROK_PID=$!

# Wait for ngrok to report its public URL
PUBLIC_URL=""
for i in $(seq 1 15); do
    PUBLIC_URL=$(curl -s http://localhost:4040/api/tunnels 2>/dev/null \
        | python3 -c "import sys,json; t=json.load(sys.stdin)['tunnels']; print(next((x['public_url'] for x in t if x['public_url'].startswith('https')), t[0]['public_url'] if t else ''))" 2>/dev/null || true)
    if [ -n "$PUBLIC_URL" ]; then
        break
    fi
    sleep 1
done

echo ""
echo "╔══════════════════════════════════════════════════╗"
echo "║           Instogram dev server running           ║"
echo "╠══════════════════════════════════════════════════╣"
printf "║  Local:   http://localhost:%-22s║\n" "$PORT"
if [ -n "$PUBLIC_URL" ]; then
printf "║  Public:  %-38s║\n" "$PUBLIC_URL"
fi
echo "╚══════════════════════════════════════════════════╝"
echo ""
echo "Enter the Public URL above in the app login screen."
echo ""
if [ -n "$MASTER_PASSWORD" ]; then
echo "  Master account: @${MASTER_USERNAME:-admin} / $MASTER_PASSWORD"
echo "  (already created if this is the first run)"
else
echo "  Tip: set MASTER_PASSWORD=yourpass to auto-create an admin account."
echo "  Example: MASTER_PASSWORD=secret ./dev-server.sh"
fi
echo ""
echo "Press Ctrl+C to stop."
echo ""

# Block until killed
wait "$SERVER_PID"
