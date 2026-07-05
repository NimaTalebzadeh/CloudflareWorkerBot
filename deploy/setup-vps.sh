#!/bin/bash
set -e

echo "=== VPS Setup for Telegram Bots (Advanced Calculator, Cloudflare Worker, YouTube Downloader) ==="

# Install Docker
if ! command -v docker &> /dev/null; then
    echo "Installing Docker..."
    curl -fsSL https://get.docker.com | sh
    systemctl enable docker
    systemctl start docker
fi

# Install Docker Compose plugin
if ! docker compose version &> /dev/null; then
    echo "Installing Docker Compose..."
    apt-get update && apt-get install -y docker-compose-plugin
fi

BOTS_DIR="/opt/bots"
mkdir -p "$BOTS_DIR"

# List of repositories
declare -A REPO_URLS
REPO_URLS["AdvancedCalculaterBot"]="https://github.com/NimaTalebzadeh/AdvancedCalculaterBot.git"
REPO_URLS["CloudflareWorkerBot"]="https://github.com/NimaTalebzadeh/CloudflareWorkerBot.git"
REPO_URLS["YouTubeDownloaderBot"]="https://github.com/NimaTalebzadeh/YouTubeDownloaderBot.git"

# Clone or update bot repositories
for repo_name in "${!REPO_URLS[@]}"; do
    repo_url="${REPO_URLS[$repo_name]}"
    repo_path="$BOTS_DIR/$repo_name"

    echo "Processing $repo_name..."
    if [ ! -d "$repo_path" ]; then
        echo "Cloning $repo_name..."
        git clone "$repo_url" "$repo_path"
    else
        echo "Repository $repo_name already exists. Pulling latest changes..."
        (cd "$repo_path" && git pull)
    fi
done

# Create .env file if not exists, or append new bot's variables
if [ ! -f "$BOTS_DIR/.env" ]; then
    echo "Creating .env file..."
    cat > "$BOTS_DIR/.env" << 'EOF'
# Advanced Calculator Bot
TELEGRAM_BOTTOKEN_CALC=your_calculator_bot_token_here
ADMIN_IDS_CALC=your_telegram_user_id_here

# Cloudflare Worker Bot
TELEGRAM_BOTTOKEN_CF=your_cloudflare_bot_token_here
ADMIN_IDS_CF=your_telegram_user_id_here

# YouTube Downloader Bot
TELEGRAM_BOTTOKEN_YTDL=your_ytdl_bot_token_here
EOF
else
    echo "Updating .env file with YouTube Downloader Bot variables if missing..."
    if ! grep -q '# YouTube Downloader Bot' "$BOTS_DIR/.env"; then
        echo -e "\n# YouTube Downloader Bot\nTELEGRAM_BOTTOKEN_YTDL=your_ytdl_bot_token_here" >> "$BOTS_DIR/.env"
    else
        echo "YouTube Downloader Bot variables already present in .env."
    fi
fi

echo ""
echo "!!! IMPORTANT: EDIT /opt/bots/.env with your real bot tokens !!!"
echo ""

# Create docker-compose.yml
echo "Creating docker-compose.yml..."
cat > "$BOTS_DIR/docker-compose.yml" << 'DOCKERCOMPOSE'
services:
  calculator-bot:
    build: ./AdvancedCalculaterBot
    container_name: advanced-calculator-bot
    restart: unless-stopped
    environment:
      - TELEGRAM_BOTTOKEN=${TELEGRAM_BOTTOKEN_CALC}
      - ADMIN_IDS=${ADMIN_IDS_CALC}
      - PORT=5001
    ports:
      - "5001:5001"

  cloudflare-bot:
    build: ./CloudflareWorkerBot
    container_name: cloudflare-worker-bot
    restart: unless-stopped
    environment:
      - TELEGRAM_BOTTOKEN=${TELEGRAM_BOTTOKEN_CF}
      - ADMIN_USER_IDS=${ADMIN_IDS_CF}
      - PORT=5002
    ports:
      - "5002:5002"

  ytdl-bot:
    build: ./YouTubeDownloaderBot
    container_name: youtube-downloader-bot
    restart: unless-stopped
    environment:
      - TELEGRAM_BOTTOKEN=${TELEGRAM_BOTTOKEN_YTDL}
      - PORT=5003
      - ASPNETCORE_URLS=http://0.0.0.0:5003
    ports:
      - "5003:5003"
DOCKERCOMPOSE

# Create auto-update script
echo "Creating auto-update.sh..."
cat > "$BOTS_DIR/auto-update.sh" << 'AUTOUPDATE'
#!/bin/bash
set -e

cd /opt/bots

REPOS=("AdvancedCalculaterBot" "CloudflareWorkerBot" "YouTubeDownloaderBot")
BOTS=("calculator-bot" "cloudflare-bot" "ytdl-bot")

for i in "${!REPOS[@]}"; do
  repo="${REPOS[$i]}"
  bot="${BOTS[$i]}"

  if [ ! -d "$repo/.git" ]; then
    echo "[$(date)] Skipping $repo - no git repo found"
    continue
  fi

  cd "$repo"
  git fetch origin
  local=$(git rev-parse HEAD)
  remote=$(git rev-parse @{u})

  if [ "$local" != "$remote" ]; then
    echo "[$(date)] Updates found for $repo. Pulling and rebuilding..."
    git pull
    cd /opt/bots
    docker compose build --no-cache "$bot"
    docker compose up -d "$bot"
  else
    cd /opt/bots
  fi
done

echo "[$(date)] Auto-update check complete"
AUTOUPDATE

chmod +x "$BOTS_DIR/auto-update.sh"

# Stop existing containers if any, then start all bots
echo "Stopping existing bot containers (if any)..."
cd "$BOTS_DIR"
docker compose down || true # '|| true' to prevent script from exiting if no containers are running

echo "Starting all bots..."
docker compose up -d

# Set up cron job for auto-update (every minute)
echo "Setting up auto-update cron job (every minute)..."
(crontab -l 2>/dev/null | grep -v auto-update.sh; echo "* * * * * $BOTS_DIR/auto-update.sh >> /var/log/bots-update.log 2>&1") | crontab -

echo ""
echo "=== Setup Complete ==="
echo "Bots directory: $BOTS_DIR"
echo "Auto-update checks every minute via cron"
echo "Update logs: /var/log/bots-update.log"
echo ""
echo "To view bot logs:"
echo "  docker logs -f advanced-calculator-bot"
echo "  docker logs -f cloudflare-worker-bot"
echo "  docker logs -f youtube-downloader-bot"
echo ""
echo "To manually trigger update:"
echo "  /opt/bots/auto-update.sh"