# CloudflareWorkerBot

A Telegram bot that deploys Cloudflare Workers (Edge Tunnel or BPB Panel) to your Cloudflare account.

## Features

- Deploy Edge Tunnel worker with KV storage and admin secret
- Deploy BPB Panel worker with KV storage and secrets (UUID, TR_PASS, SUB_PATH)
- Manage workers: list, status, delete, analytics
- Manage KV namespaces: list keys, get, put, delete
- Account info and destroy all resources
- Simple conversation flow to gather required configuration
- Deployable via Docker (including on Railway)

## Prerequisites

- [.NET SDK 10.0](https://dotnet.microsoft.com/download) (for local development)
- [Docker](https://www.docker.com/products/docker-desktop) (for containerized deployment)
- A Telegram bot token (from [@BotFather](https://t.me/BotFather))
- A Cloudflare API token with permissions:
  - Workers Scripts: Edit
  - Workers KV Storage: Edit
  - Account: Read
  - Users: Read

## Setup

### 1. Create a Telegram Bot

Talk to [@BotFather](https://t.me/BotFather) on Telegram and create a new bot. Copy the token.

### 2. Configure Environment Variables

Create a `.env` file in the project root (for local development) or set environment variables directly:

```dotenv
TELEGRAM_BOTTOKEN=your_telegram_bot_token_here
# Optional: Logging level
# LOG_LEVEL=Information
```

**Important**: Never commit your actual token to version control! The `appsettings.json` file is configured to be ignored by git and should contain an empty string for the token.

### 3. Run Locally

```bash
dotnet run
```

### 4. Run with Docker

```bash
docker build -t cloudflareworkerbott .
docker run -d --name cloudflareworkerbot \
  -e TELEGRAM_BOTTOKEN=your_telegram_bot_token_here \
  -p 5000:80 \
  cloudflareworkerbott
```

## Configuration

The bot reads configuration from environment variables (with fallback to `appsettings.json` for non-secret settings). The following environment variables are supported:

| Variable | Description |
|----------|-------------|
| `TELEGRAM_BOTTOKEN` | Your Telegram bot token (required) |
| `ASPNETCORE_ENVIRONMENT` | Environment name (default: Production) |
| `DOTNET_ENVIRONMENT` | Same as above |

The `appsettings.json` file should contain an empty string for the BotToken to avoid committing secrets, but the actual value should come from environment variables:

```json
{
  "Telegram": {
    "BotToken": ""  // Keep empty, set via TELEGRAM_BOTTOKEN env var
  },
  // ... other settings
}
```

Logging configuration can be adjusted in `appsettings.json` (not committed to repository).

## Deployment to Railway

1. Fork this repository.
2. Create a new Railway project and connect your fork.
3. Add the environment variable `TELEGRAM_BOTTOKEN` with your actual token in the Railway dashboard.
4. Railway will automatically build and deploy using the provided Dockerfile.
5. Ensure the allocated port matches the port the app listens on (default 5000, set via `PORT` env var if needed).

## How It Works

1. User starts the bot with `/start`.
2. Bot asks to choose between Edge Tunnel or BPB Panel.
3. Bot guides the user to create a Cloudflare API token with required permissions (via a pre-filled link).
4. User provides the API token.
5. Bot fetches the user's Cloudflare accounts and asks for selection (if multiple).
6. For BPB, bot generates an admin secret; for Edge Tunnel, user provides admin secret.
7. Bot deploys the worker script, creates a KV namespace, binds it, and sets necessary secrets via Cloudflare API (or wrangler in Docker).
8. Bot provides the worker URL and instructions.

## Architecture

- **.NET 10** minimal API with Telegram.Bot for long polling.
- **Cloudflare API** integration via REST calls.
- **Worker scripts** embedded as static files (`edge_tunnel.js`, `bpb.js`).
- **Dependency injection** for services.
- **Structured logging** with Serilog.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- [Telegram.Bot](https://github.com/TelegramBots/telegrambot.dotnet)
- [Cloudflare API](https://api.cloudflare.com/)
- Wrangler (used in Docker image for secret setting)