# WebApp Portfolio

A self-hosted, database-driven developer portfolio built with **Blazor Server (.NET 10)**, **Entity Framework Core**, and **PostgreSQL**. Fully manageable through a built-in admin panel — no code changes needed to update content.

## Features

**Public Portfolio**
- Hero section with GitHub profile picture, bio, tagline, CTA buttons, and resume link
- Experience timeline
- Projects showcase with tech stack tags, live demo and repo links
- Interactive tech constellation — skills visualized as star systems with glowing connections
- GitHub section — profile banner, pinned repos, all public repos tab, contribution graph with streak tracking
- Contact / hiring message form
- Collaboration network — public request form + approved collaborator carousel

**Admin Panel** (`/admin`)
- Secure login with BCrypt password hashing + forgot/reset password via email
- Dashboard with at-a-glance stats
- Full CRUD for profile, experiences, skills, and projects
- Inbox for hiring messages with read/unread tracking
- Collaboration request management (approve / reject / flag)
- GitHub cache refresh

## Tech Stack

| Layer | Technology |
|-------|------------|
| Framework | ASP.NET Core / Blazor Server (.NET 10) |
| ORM | Entity Framework Core |
| Database | PostgreSQL |
| Styling | Custom CSS (dark theme) |
| Auth | Custom session-based admin auth + BCrypt |
| Email | Resend API / MailKit |
| GitHub | GitHub GraphQL + REST API |
| Deployment | Docker / Docker Compose |

## Getting Started

### 1. Clone the repo

```bash
git clone https://github.com/bullet162/WebApp-Portfolio.git
cd WebApp-Portfolio
```

### 2. Configure settings

Copy the example config and fill in your values:

```bash
cp BlazorPortfolio/appsettings.example.json BlazorPortfolio/appsettings.json
```

Key fields in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=portfolio;Username=postgres;Password=yourpassword"
  },
  "Admin": {
    "Username": "your-admin-username",
    "Password": "your-admin-password"
  },
  "GitHub": {
    "Token": "your-github-token"
  },
  "Resend": {
    "ApiKey": "your-resend-api-key"
  }
}
```

> GitHub token is optional — the app falls back to the REST API for public repos if no token is provided. A token is required for pinned repos and the contribution graph.

### 3. Apply migrations and run

```bash
cd BlazorPortfolio
dotnet ef database update
dotnet run
```

The app will be available at `https://localhost:5001`.

### Docker

```bash
docker compose up --build
```

## Admin Panel

Navigate to `/admin/login`. On first run, credentials are set via `appsettings.json` (or environment variables in production using the `__` convention, e.g. `Admin__Password`).

## Environment Variables (Production)

| Variable | Description |
|----------|-------------|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string |
| `Admin__Username` | Admin login username |
| `Admin__Password` | Admin login password |
| `GitHub__Token` | GitHub personal access token |
| `Resend__ApiKey` | Resend API key for email |
| `KeepAlive__BaseUrl` | Health check URL (for Render free tier) |

## Keep-Alive (Render Free Tier)

Render's free tier spins down services after ~15 minutes of inactivity, causing slow cold starts for the first visitor.

This repo includes two mechanisms to combat that:

**1. GitHub Actions pinger** (`.github/workflows/keepalive.yml`)
A scheduled workflow that hits the `/health` endpoint every 14 minutes, Monday–Friday 6am–12am Philippine Time (UTC+8). This keeps the app warm during active hours without any third-party service.

To use it in your own fork, just push the repo — GitHub picks up the workflow automatically. No secrets or extra configuration needed.

**2. WarmUpService** (`BlazorPortfolio/Services/WarmUpService.cs`)
A hosted background service that runs 5 seconds after the app boots. It pre-loads all portfolio and GitHub data into the server-side memory cache in parallel, so the first real visitor gets a fast page load even after a cold start.

To configure the keep-alive URL, set the `KeepAlive__BaseUrl` environment variable to your deployed app's health endpoint:

```
KeepAlive__BaseUrl=https://your-app.onrender.com/health
```

> If you fork this repo, update the URL in `.github/workflows/keepalive.yml` to point to your own Render deployment.

## Mobile Performance

The app includes several optimizations specifically for mobile devices and the Render free tier:

- **Adaptive animations** — all decorative animations (starfield, avatar ring spin, card entrance effects, hero fade-ins, floating orbs) are disabled on screens ≤ 768px via CSS media queries. Desktop keeps the full animated experience.
- **Starfield JS skipped on mobile** — the canvas animation loop never starts on mobile, saving CPU and battery entirely.
- **Static gradient background on mobile** — replaces the canvas starfield with a lightweight CSS radial gradient.
- **GitHub data loading** — cache-first with background fill. Cached data renders immediately; missing data fetches in the background after first paint. GitHub cache TTL is 2 hours.
- **SignalR tuned for mobile** — handshake timeout 30s, keep-alive interval 15s, client timeout 60s to tolerate unstable mobile networks.
- **Static file caching** — JS/CSS cached 1 hour with `must-revalidate`, images 1 day, WASM 7 days with `immutable`.
- **Debounced Blazor update handlers** — all `blazor:afterUpdate` listeners are debounced to prevent excessive repaints on every interaction.

## License

MIT
