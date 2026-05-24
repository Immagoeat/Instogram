#!/usr/bin/env bash
# Instogram VPS deploy script
# Usage: ./deploy.sh <your-server-ip-or-domain>
# Requires: ssh access as root or sudo user, Docker installed on the server

set -e

SERVER="${1:?Usage: ./deploy.sh <server-ip-or-domain>}"
REMOTE_DIR="/opt/instogram"
JWT_KEY="${JWT_KEY:-$(openssl rand -base64 32)}"

echo "=== Deploying Instogram to $SERVER ==="
echo "JWT key: $JWT_KEY  (save this — you'll need it if you redeploy)"

# 1. Ensure Docker is installed on the server
ssh "root@$SERVER" bash <<'SETUP'
  if ! command -v docker &>/dev/null; then
    apt-get update -qq
    apt-get install -y -qq docker.io docker-compose-plugin
    systemctl enable --now docker
  fi
  mkdir -p /opt/instogram
SETUP

# 2. Copy server source + compose file
rsync -az --exclude='bin' --exclude='obj' \
  InstogramServer/ "root@$SERVER:$REMOTE_DIR/InstogramServer/"
scp docker-compose.yml "root@$SERVER:$REMOTE_DIR/"

# 3. Write production env file on the server
ssh "root@$SERVER" bash <<ENVFILE
  cat > $REMOTE_DIR/.env <<EOF
JWT_KEY=$JWT_KEY
EOF
ENVFILE

# 4. Build and start
ssh "root@$SERVER" bash <<DEPLOY
  cd $REMOTE_DIR
  docker compose pull 2>/dev/null || true
  docker compose up --build -d
  docker compose ps
DEPLOY

# 5. Install nginx if requested and set up reverse proxy
read -rp "Set up nginx reverse proxy with your domain? (y/n): " SETUP_NGINX
if [[ "$SETUP_NGINX" == "y" ]]; then
  read -rp "Domain name (e.g. instogram.example.com): " DOMAIN

  ssh "root@$SERVER" bash <<NGINX
    apt-get install -y -qq nginx certbot python3-certbot-nginx
    cat > /etc/nginx/sites-available/instogram <<'NGINXCONF'
server {
    listen 80;
    server_name $DOMAIN;

    location / {
        proxy_pass         http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade \$http_upgrade;
        proxy_set_header   Connection keep-alive;
        proxy_set_header   Host \$host;
        proxy_set_header   X-Real-IP \$remote_addr;
        proxy_set_header   X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto \$scheme;
        proxy_cache_bypass \$http_upgrade;

        # SignalR WebSocket support
        proxy_read_timeout 3600;
        proxy_send_timeout 3600;
    }
}
NGINXCONF
    ln -sf /etc/nginx/sites-available/instogram /etc/nginx/sites-enabled/instogram
    nginx -t && systemctl reload nginx
    certbot --nginx -d $DOMAIN --non-interactive --agree-tos -m admin@$DOMAIN || true
NGINX

  SERVER_URL="https://$DOMAIN"
  echo ""
  echo "=== Done! Server URL: $SERVER_URL ==="
else
  SERVER_URL="http://$SERVER:5000"
  echo ""
  echo "=== Done! Server URL: $SERVER_URL ==="
fi

echo "Set this as the server URL in the Instogram app login screen."
echo "JWT Key (keep this secret): $JWT_KEY"
