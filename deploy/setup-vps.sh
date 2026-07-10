#!/bin/bash
set -e

BOTS_DIR="/opt/bots"
mkdir -p "$BOTS_DIR"

# 1. Define your 4 bots
REPOS=("AdvancedCalculaterBot" "CloudflareWorkerBot" "TelegramSemanticSearch" "YouTubeDownloaderBot")
BOTS=("calculator-bot" "cloudflare-bot" "semantic-search-bot" "ytdl-bot")
GIT_URLS=("https://github.com/NimaTalebzadeh/AdvancedCalculaterBot.git" "https://github.com/NimaTalebzadeh/CloudflareWorkerBot.git" "https://github.com/NimaTalebzadeh/TelegramSemanticSearch.git" "https://github.com/NimaTalebzadeh/YouTubeDownloaderBot.git")

# 2. Clone/Init Git repositories
for i in "${!REPOS[@]}"; do
    repo="${REPOS[$i]}"
    url="${GIT_URLS[$i]}"
    if [ ! -d "$BOTS_DIR/$repo" ]; then
        echo "Cloning $repo..."
        git clone "$url" "$BOTS_DIR/$repo"
    fi
done

# 3. Create/Update .env file
cat > "$BOTS_DIR/.env" << 'EOF'
# Advanced Calculator Bot
TELEGRAM_BOTTOKEN_CALC=your_calc_token
ADMIN_IDS_CALC=your_admin_id

# Cloudflare Worker Bot
TELEGRAM_BOTTOKEN_CF=your_cf_token
ADMIN_IDS_CF=your_admin_id

# YouTube Downloader Bot
TELEGRAM_BOTTOKEN_YTDL=your_youtubedownloader_token

# Semantic Search Bot
TELEGRAM_BOTTOKEN_SEMANTIC=your_semantic_token
MTPROTO_API_ID=your_api_id
MTPROTO_API_HASH=your_api_hash
MTPROTO_PHONE_NUMBER=your_phone_number
EOF

# 4. Create the shared docker-compose.yml
cat > "$BOTS_DIR/docker-compose.yml" << 'EOF'
services:
  calculator-bot:
    build: ./AdvancedCalculaterBot
    container_name: advanced-calculator-bot
    restart: unless-stopped
    environment:
      - TELEGRAM_BOTTOKEN=${TELEGRAM_BOTTOKEN_CALC}
      - ADMIN_IDS=${ADMIN_IDS_CALC}
    ports: ["5001:5001"]

  cloudflare-bot:
    build: ./CloudflareWorkerBot
    container_name: cloudflare-worker-bot
    restart: unless-stopped
    environment:
      - TELEGRAM_BOTTOKEN=${TELEGRAM_BOTTOKEN_CF}
      - ADMIN_IDS=${ADMIN_IDS_CF}
    ports: ["5002:5002"]

  semantic-search-bot:
    build: ./TelegramSemanticSearch
    container_name: semantic-search-bot
    restart: unless-stopped
    environment:
      - TELEGRAM_BOTTOKEN=${TELEGRAM_BOTTOKEN_SEMANTIC}
      - MTPROTO_API_ID=${MTPROTO_API_ID}
      - MTPROTO_API_HASH=${MTPROTO_API_HASH}
      - MTPROTO_PHONE_NUMBER=${MTPROTO_PHONE_NUMBER}
    ports: ["5003:5003"]

  ytdl-bot:
    build: ./YouTubeDownloaderBot
    container_name: youtube-downloader-bot
    restart: unless-stopped
    environment:
      - TELEGRAM_BOTTOKEN=${TELEGRAM_BOTTOKEN_YTDL}
    ports: ["5004:5004"]
EOF

# 5. Create auto-update script
cat > "$BOTS_DIR/auto-update.sh" << 'AUTOUPDATE'
#!/bin/bash
set -e

cd /opt/bots

REPOS=("AdvancedCalculaterBot" "CloudflareWorkerBot" "TelegramSemanticSearch" "YouTubeDownloaderBot")
BOTS=("calculator-bot" "cloudflare-bot" "semantic-search-bot" "ytdl-bot")

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

# 6. Set up cron job for auto-update (every minute)
(crontab -l 2>/dev/null | grep -v auto-update; echo "* * * * * $BOTS_DIR/auto-update.sh >> /var/log/bots-update.log 2>&1") | crontab -

# 7. Apply changes
cd "$BOTS_DIR"
docker compose up -d --build
