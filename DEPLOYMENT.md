# Deploying BlazorPortfolio to Render (Free Tier)

This guide walks you through deploying BlazorPortfolio to [Render](https://render.com) using the free web service tier with a persistent disk for SQLite storage.

---

## Prerequisites

- A [Render](https://render.com) account (free)
- Your GitHub repository connected to Render
- A [Resend](https://resend.com) API key (for contact form emails)
- A GitHub personal access token (for fetching repos on the portfolio)

---

## Required Environment Variables

Set these in the Render dashboard under **Environment** for your web service.

| Variable | Description | Example |
|---|---|---|
| `Admin__Username` | Admin login username | `admin` |
| `Admin__Password` | Admin login password | `changeme123` |
| `GitHub__Token` | GitHub personal access token (read:user, public_repo) | `ghp_xxxxxxxxxxxx` |
| `Resend__ApiKey` | Resend API key for sending emails | `re_xxxxxxxxxxxx` |
| `KeepAlive__BaseUrl` | Full URL to the `/health` endpoint of your deployed app | `https://your-app.onrender.com/health` |
| `KeepAlive__IntervalMinutes` | Ping interval in minutes (minimum 10, recommended 14) | `14` |
| `ConnectionStrings__DefaultConnection` | Neon/Supabase Postgres connection string | `Host=ep-xxx.neon.tech;Database=portfolio;Username=user;Password=pass;SSL Mode=Require` |

> The double-underscore (`__`) convention maps to nested JSON keys — e.g., `Admin__Username` sets `Admin:Username` at runtime.

---

## Free PostgreSQL Database (Neon)

The app now uses PostgreSQL instead of SQLite. Get a free database from [Neon](https://neon.tech):

1. Sign up at [neon.tech](https://neon.tech) (free tier, no credit card needed)
2. Create a new project — pick the **Singapore** region to match your Render service
3. Copy the connection string from the dashboard (it looks like `postgresql://user:pass@ep-xxx.neon.tech/neondb?sslmode=require`)
4. Convert it to the Npgsql format: `Host=ep-xxx.neon.tech;Database=neondb;Username=user;Password=pass;SSL Mode=Require`
5. Set this as `ConnectionStrings__DefaultConnection` in Render's environment variables

---

## Step-by-Step Setup

### 1. Connect your GitHub repo to Render

1. Log in to [Render](https://render.com) and go to your dashboard.
2. Click **New +** → **Web Service**.
3. Select **Build and deploy from a Git repository** and click **Next**.
4. Connect your GitHub account if you haven't already, then select this repository.

### 2. Configure the web service

On the service configuration screen:

- **Name**: choose a name (this becomes part of your URL, e.g., `your-app.onrender.com`)
- **Region**: pick the region closest to you
- **Branch**: `main`
- **Runtime**: select **Docker** — Render will use the `Dockerfile` at the repo root
- **Instance Type**: **Free**

Leave the build and start commands blank — the Dockerfile handles everything.

### 3. Set environment variables

1. Scroll to the **Environment Variables** section.
2. Add each variable from the table above.
3. For `KeepAlive__BaseUrl`, use a placeholder for now (e.g., `https://placeholder.onrender.com/health`) — you'll update it after the first deploy.

### 4. Deploy

Click **Create Web Service**. Render will:

1. Clone your repository
2. Build the Docker image using the `Dockerfile`
3. Mount the persistent disk at `/data`
4. Inject your environment variables
5. Start the container

The first build takes a few minutes. You can watch the progress in the **Logs** tab.

### 5. Update `KeepAlive__BaseUrl`

Once the service is live, copy your app's URL from the Render dashboard (e.g., `https://your-app.onrender.com`) and update the `KeepAlive__BaseUrl` environment variable:

```
https://your-app.onrender.com/health
```

Go to your service → **Environment** → edit `KeepAlive__BaseUrl` → **Save Changes**. Render will automatically redeploy with the updated value.

---

## Auto-Deploy on Push

Render automatically redeploys your app whenever you push to the `main` branch. No manual steps needed — just push and the new image will be built and deployed.

If a deployment fails, Render keeps the previously running container live so your portfolio stays accessible.

---

## Verifying the Deployment

Once deployed, check that everything is working:

- Visit `https://your-app.onrender.com/health` — should return `OK`
- Visit `https://your-app.onrender.com` — your portfolio should load
- Log in at `/admin/login` using your `Admin__Username` and `Admin__Password`

---

## Local Development

For local development, copy `appsettings.example.json` to `appsettings.json` and fill in your values:

```bash
cp BlazorPortfolio/appsettings.example.json BlazorPortfolio/appsettings.json
```

The `appsettings.json` file is gitignored and will never be committed.
