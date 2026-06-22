#!/usr/bin/env bash
# Start Instogram server locally + expose it via ngrok
# Usage: ./dev-server.sh
# Optional env vars:
#   PORT=5000          override the server port
#   JWT_KEY=...        use a specific JWT key (generated each run otherwise)

set -e

PORT="${PORT:-5001}"
JWT_KEY="${JWT_KEY:-$(openssl rand -base64 32)}"

# Kill background jobs on exit
cleanup() {
    kill "$SERVER_PID" "$NGROK_PID" 2>/dev/null || true
}
trap cleanup EXIT INT TERM

echo "=== Instogram Dev Server ==="
echo ""

# Start the server
cd "$(dirname "$0")/InstogramServer"
ASPNETCORE_URLS="http://localhost:$PORT" \
Jwt__Key="$JWT_KEY" \
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
echo "║  Local:   http://localhost:$PORT                   ║"
if [ -n "$PUBLIC_URL" ]; then
echo "║  Public:  $PUBLIC_URL"
fi
echo "╚══════════════════════════════════════════════════╝"
echo ""
echo "Enter the Public URL above in the app login screen."
echo "Press Ctrl+C to stop."
echo ""

# Block until killed
wait "$SERVER_PID"
