#!/bin/bash
set -e

cd "$(dirname "$0")"

REPOS=("AdvancedCalculaterBot" "CloudflareWorkerBot" "YoutubeDownloaderBot)
BOTS=("calculator-bot" "cloudflare-bot" "ytdl-bot)

for i in "${!REPOS[@]}"; do
  repo="${REPOS[$i]}"
  bot="${BOTS[$i]}"
  repo_path="../$repo"

  if [ ! -d "$repo_path/.git" ]; then
    echo "[$(date)] Skipping $repo - no git repo found"
    continue
  fi

  cd "$repo_path"

  git fetch origin
  local=$(git rev-parse HEAD)
  remote=$(git rev-parse @{u})

  if [ "$local" != "$remote" ]; then
    echo "[$(date)] Updates found for $repo. Pulling and rebuilding..."
    git pull
    cd "$OLDPWD"
    docker compose build --no-cache "$bot"
    docker compose up -d "$bot"
  else
    cd "$OLDPWD"
  fi
done

echo "[$(date)] Auto-update check complete"