# JarMonitor

A monitoring tool for Monobank jars (donation jars) that generates daily reports with progress tracking and sends them to Telegram.

## Features

- **Monobank Integration**: Fetches jar data via Monobank's public API
- **Daily Reports**: Generates reports showing collected amounts, goals, and progress
- **Daily Change Tracking**: Calculates and displays how much was collected since the previous report
- **Progress Visualization**: Text-based progress bars and SVG charts
- **Telegram Notifications**: Sends formatted reports with charts to a Telegram channel
- **Scheduled Execution**: Runs automatically at a configured time using cron-style scheduling
- **History Storage**: Keeps up to 90 days of historical data in JSON format
- **Timezone Support**: Configurable timezone for scheduling and date display

## Running Modes

### Scheduled Mode (Default)

Runs continuously and executes reports at the configured schedule time:

```bash
dotnet run
```

### Immediate Mode

Runs a single report immediately and exits:

```bash
dotnet run -- --run-now
```

## Configuration

Configuration is loaded from `appsettings.json` and can be overridden with environment variables.

### appsettings.json

```json
{
    "Timezone": "Europe/Kyiv",
    "ScheduleTime": "23:59",
    "Jars": [
        {
            "Name": "SAMPLE_NAME",
            "JarId": "SAMPLE_VALUE"
        }
    ],
    "Telegram": {
        "BotToken": "BOT_TOKEN",
        "ChannelId": "CHANNEL_ID"
    }
}
```

### Configuration Options

| Option | Description | Default |
|--------|-------------|---------|
| `Timezone` | IANA timezone for scheduling and dates | `UTC` |
| `ScheduleTime` | Time to run daily report (HH:mm format) | `00:00` |
| `Jars` | Array of jars to monitor | Required |
| `Jars[].Name` | Display name for the jar | Required |
| `Jars[].JarId` | Monobank jar ID (from jar URL) | Required |
| `Telegram.BotToken` | Telegram bot API token | Required |
| `Telegram.ChannelId` | Telegram channel/chat ID to send reports | Required |

### Environment Variables

All settings can be overridden using environment variables:

```bash
Timezone=Europe/Kyiv
ScheduleTime=08:00
Telegram__BotToken=your-bot-token
Telegram__ChannelId=your-channel-id
```

## Docker

> **Note:** Pre-built images target **linux/arm64** architecture (Raspberry Pi, Apple Silicon, etc.). For x86_64/amd64, build the image locally.

### Docker Compose (Recommended)

Create a `docker-compose.yml`:

```yaml
services:
  jarmonitor:
    image: ghcr.io/mishamyte/jarmonitor:latest
    container_name: jarmonitor
    restart: unless-stopped
    volumes:
      - ./data:/app/data
    environment:
      - TZ=Europe/Kyiv
      - Timezone=Europe/Kyiv
      - ScheduleTime=23:59
      # Jars (add more with Jars__1__, Jars__2__, etc.)
      - Jars__0__Name=My Jar
      - Jars__0__JarId=ABC123xyz
      # Telegram
      - Telegram__BotToken=your-bot-token
      - Telegram__ChannelId=your-channel-id
```

Run:

```bash
docker compose up -d
```

Check logs:

```bash
docker compose logs -f jarmonitor
```

### Using GitHub Container Registry

```bash
docker pull ghcr.io/mishamyte/jarmonitor:latest
```

### Build Locally

```bash
docker build -t jarmonitor -f JarMonitor/Dockerfile .
```

### Run with Docker

```bash
docker run -d \
  -v $(pwd)/data:/app/data \
  -e Telegram__BotToken=your-bot-token \
  -e Telegram__ChannelId=your-channel-id \
  jarmonitor
```

## Data Storage

History is stored in `data/history.json` relative to the working directory. Mount this as a volume in Docker to persist data across container restarts.

## Getting Jar ID

1. Open the Monobank jar in a browser
2. The URL will look like: `https://send.monobank.ua/jar/ABC123xyz`
3. The jar ID is the last part: `ABC123xyz`

## Tech Stack

- **F#** / .NET 10
- **Cronos** - Cron expression parsing for scheduling
- **FsHttp** - HTTP client for API requests
- **Funogram** - Telegram Bot API wrapper
- **SkiaSharp** - Chart rendering (SVG to PNG)
- **Svg.Skia** - SVG parsing and rendering
