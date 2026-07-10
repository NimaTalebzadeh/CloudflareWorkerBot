#!/bin/bash
set -e

BOTS_DIR="/opt/bots"
mkdir -p "$BOTS_DIR"

# 1. Define your 4 bots
REPOS=("AdvancedCalculaterBot" "CloudflareWorkerBot" "TelegramSemanticSearch" "YoutubeDownloaderBot")
BOTS=("calculator-bot" "cloudflare-bot" "semantic-search-bot" "youtube-downloader-bot")

# 2. Clone/Init Git repositories
for repo in "${REPOS[@]}"; do
    if [ ! -d "$BOTS_DIR/$repo" ]; then
        echo "Cloning $repo..."
        # Replace these with your actual git URLs
        git clone "https://github.com/NimaTalebzadeh/AdvancedCalculaterBot.git" "$BOTS_DIR/$repo"
        git clone "https://github.com/NimaTalebzadeh/CloudflareWorkerBot.git" "$BOTS_DIR/$repo"
        git clone "https://github.com/NimaTalebzadeh/YouTubeDownloaderBot.git" "$BOTS_DIR/$repo"
        git clone "https://github.com/NimaTalebzadeh/TelegramSemanticSearch.git" "$BOTS_DIR/$repo"
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
      - MTPROTO__APIID=${MTPROTO_API_ID}
      - MTPROTO__APIHASH=${MTPROTO_API_HASH}
      - MTPROTO__PHONENUMBER=${MTPROTO_PHONE_NUMBER}
    ports: ["5003:5003"]

  fourth-bot:
    build: ./FourthBotName
    container_name: fourth-bot
    restart: unless-stopped
    environment:
      - TELEGRAM_BOTTOKEN=${TELEGRAM_BOTTOKEN_FOURTH}
    ports: ["5004:5004"]
EOF

# 5. Apply changes
cd "$BOTS_DIR"
docker compose up -d --build
