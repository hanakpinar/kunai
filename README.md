# Kunai

> Read. Watch. Play. Hit the mark.

Kunai is a personal media tracker: books, movies, TV series, games, anime and manga on one shelf, with a public profile, yearly goals and a ninja belt that grows as you finish things.

This repository contains the **HTTP API** that the web app and the mobile apps talk to.

## Tech stack

| Area | Choice |
|---|---|
| Runtime | .NET 10 (LTS), C# 14 |
| API | ASP.NET Core Minimal APIs, vertical slices |
| Database | PostgreSQL 18, EF Core 10 |
| Local orchestration | Aspire 17 (Postgres, pgAdmin, Mailpit) |
| Observability | OpenTelemetry (logs, traces, metrics) |
| Quality gates | Nullable, warnings as errors, .NET analyzers + Meziantou, central package management, NuGet audit |
| CI | GitHub Actions, Dependabot |

## Getting started

Prerequisites:

- .NET SDK 10.0.404 or newer (pinned in `global.json`)
- Docker Desktop (or another Docker-compatible runtime) running

Start everything (database, pgAdmin, Mailpit and the API) with one command:

```bash
dotnet run --project src/Kunai.AppHost
```

The terminal prints a link to the Aspire dashboard. From there you can open the API, pgAdmin and the Mailpit inbox, and follow logs and traces.

## Repository layout

```
src/
  Kunai.AppHost/          Aspire orchestration for local development
  Kunai.ServiceDefaults/  OpenTelemetry, health checks, service discovery
  Kunai.Api/              The HTTP API
.github/
  workflows/ci.yml        Restore, format check, build
  dependabot.yml          Weekly NuGet, .NET SDK and GitHub Actions updates
```

## Health endpoints

| Path | Meaning |
|---|---|
| `/health/live` | The process is up (a failure restarts the container) |
| `/health/ready` | The app can serve traffic (a failure stops routing traffic to it) |
