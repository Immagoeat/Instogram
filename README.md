# Instogram

A privacy-first social media desktop app and server built with Avalonia UI + ASP.NET Core 9.

## Features

- **Posts** — text posts with hashtags, likes, and comments
- **Stories** — 24-hour disappearing stories with customisable backgrounds
- **Feed** — live-updating following feed (real-time via SignalR)
- **Explore** — discover posts by tag from the whole community
- **Profiles** — bio, website, accent colour, follow/unfollow
- **Direct Messages** — one-on-one and group conversations with live typing indicators
- **Group chats** — create groups, add members, rename chats
- **Audio calls** — real-time voice calling via WebRTC signalling + PortAudio
- **Friend requests** — send, accept, and decline friend requests
- **Notifications** — live badge for DMs, likes, comments, follows, friend requests
- **Themes** — 6 built-in colour themes (OLED Black, Dark, Light, Purple Night, Ocean Blue, Sunset)
- **Secure accounts** — BCrypt passwords, CAPTCHA on registration
- **Server mode** — connect any device to one shared server; all data lives server-side

## Architecture

```
InstogramApp/        Avalonia desktop GUI (net9.0)
InstogramServer/     ASP.NET Core 9 API + SignalR hub
dependencies/        Shared local-mode models (compiled into InstogramApp)
```

## Tech stack

| Layer | Technology |
|---|---|
| Desktop UI | Avalonia UI 12 |
| MVVM | CommunityToolkit.Mvvm 8 |
| Server framework | ASP.NET Core 9 Minimal APIs |
| Real-time | ASP.NET Core SignalR |
| Database | EF Core 9 + SQLite |
| Auth | JWT Bearer (30-day tokens) |
| Passwords | BCrypt.Net-Next |
| Local storage | AES-256-GCM encrypted `.igdb` files |
| Audio | PortAudioSharp2 |
| Language | C# 13 / .NET 9 |

---

## Running locally (offline mode)

```bash
cd InstogramApp
dotnet run
```

Leave the server URL field empty. Data is saved locally in `InstogramApp/data/instogram.igdb`, encrypted with a machine-specific key.

---

## Running the server

### Development

```bash
cd InstogramServer
dotnet run
```

The server starts on `http://localhost:5000` by default. From the app, enter `http://localhost:5000` as the server URL.

### Production (Linux VPS)

#### 1. Install .NET 9

```bash
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 9.0
echo 'export PATH=$HOME/.dotnet:$PATH' >> ~/.bashrc
source ~/.bashrc
```

#### 2. Publish the server

```bash
cd InstogramServer
dotnet publish -c Release -o /opt/instogram
```

#### 3. Set the JWT secret

Edit `/opt/instogram/appsettings.json` and change `Jwt:Key` to a long random string:

```json
{
  "Jwt": {
    "Key": "REPLACE_WITH_64_RANDOM_CHARS_AT_MINIMUM",
    "Issuer": "InstogramServer",
    "Audience": "InstogramApp"
  },
  "ConnectionStrings": {
    "Default": "Data Source=/var/lib/instogram/instogram.db"
  }
}
```

#### 4. Create a systemd service

```ini
# /etc/systemd/system/instogram.service
[Unit]
Description=Instogram Server
After=network.target

[Service]
WorkingDirectory=/opt/instogram
ExecStart=/root/.dotnet/dotnet InstogramServer.dll
Restart=always
RestartSec=5
Environment=ASPNETCORE_URLS=http://0.0.0.0:5000
Environment=ASPNETCORE_ENVIRONMENT=Production
User=www-data

[Install]
WantedBy=multi-user.target
```

```bash
mkdir -p /var/lib/instogram
chown www-data:www-data /var/lib/instogram
systemctl daemon-reload
systemctl enable --now instogram
```

#### 5. Reverse proxy with nginx (optional, recommended for HTTPS)

```nginx
server {
    listen 80;
    server_name instogram.yourdomain.com;
    return 301 https://$host$request_uri;
}

server {
    listen 443 ssl;
    server_name instogram.yourdomain.com;

    ssl_certificate     /etc/letsencrypt/live/instogram.yourdomain.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/instogram.yourdomain.com/privkey.pem;

    location / {
        proxy_pass         http://127.0.0.1:5000;
        proxy_http_version 1.1;
        # Required for SignalR WebSockets
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_read_timeout 86400;
    }
}
```

Get a free certificate:

```bash
apt install certbot python3-certbot-nginx
certbot --nginx -d instogram.yourdomain.com
```

#### 6. Connect from the desktop app

Launch the app, click **"Connect to server"**, enter your server URL (`https://instogram.yourdomain.com`), then log in or register.

---

## Docker (alternative)

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY InstogramServer/ .
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000
ENTRYPOINT ["dotnet", "InstogramServer.dll"]
```

```bash
docker build -t instogram-server -f Dockerfile .
docker run -d -p 5000:5000 \
  -v instogram-data:/app \
  -e Jwt__Key="your_secret_key" \
  --name instogram instogram-server
```

---

## Local storage format

When running in offline mode, all data is saved to `data/instogram.igdb`.  
Wire format: `[12-byte nonce][16-byte GCM tag][ciphertext]`  
Key derivation: PBKDF2-SHA256, 100,000 iterations, seed = machine ID + app salt.
