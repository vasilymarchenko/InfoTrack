# InfoTrack Solicitor Intelligence Tool

A .NET API + Vue 3 SPA that scrapes conveyancing solicitor listings from [solicitors.com](https://www.solicitors.com), de-duplicates firms across locations, and returns a structured report with sales insights (top firms by review count, multi-location chains, coverage gaps, contactability). Search runs are persisted to Postgres; subsequent calls can retrieve stored results, compare runs with confidence-rated change detection, or query the current state of every known firm. The SPA is co-hosted — served by the API as static files from `wwwroot` — so the whole stack runs from a single container with no CORS configuration needed.

## Prerequisites

| Tool | Version |
|------|---------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.x |
| [Node.js](https://nodejs.org/) | 22.x (only needed for local SPA dev; Docker handles it automatically) |
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | required for the Docker workflow |

Outbound HTTPS to `www.solicitors.com` is required when calling `POST /api/searches` (parser tests run offline against saved HTML fixtures).

## Run with Docker

From the repository root:

```bash
docker compose up --build
```

`docker compose up` starts two services:

1. **`db`** — Postgres 17. Data is stored in a named Docker volume (`pgdata`), so it survives container restarts.
2. **`api`** — the .NET API with the pre-built Vue SPA bundled inside. It waits for the `db` healthcheck to pass, then applies any pending EF migrations automatically on startup. No manual database setup is required.

The Docker build is multi-stage: a Node stage builds the SPA (`web/`), the .NET stage copies the compiled assets into `wwwroot/`, and publishes the API. The SPA and API share the same origin (`http://localhost:8080`), so no CORS configuration is needed.

The API listens on **http://localhost:8080**.

| URL | Purpose |
|-----|---------|
| http://localhost:8080/ | Vue SPA (solicitor search UI) |
| http://localhost:8080/health | Liveness check |
| http://localhost:8080/scalar/v1 | Interactive API docs (Scalar) |
| http://localhost:8080/openapi/v1.json | OpenAPI document |

Stop the stack:

```bash
docker compose down
```

To also delete the Postgres data volume:

```bash
docker compose down -v
```

Rebuild after code changes:

```bash
docker compose up --build
```

## Build, test, and run without Docker

All commands below are run from the repository root.

### Build

```bash
dotnet build InfoTrack.slnx
```

### Test

```bash
dotnet test tests/InfoTrack.Tests/InfoTrack.Tests.csproj
```

Parser tests load embedded HTML fixtures from `tests/InfoTrack.Tests/Fixtures/` and do not call the live site. Change-detection and projection service tests are pure (no database required).

### Run the API

A local Postgres instance is required. Set the connection string via environment variable before running:

```bash
# PowerShell
$env:ConnectionStrings__Postgres = "Host=localhost;Port=5432;Database=infotrack;Username=infotrack;Password=infotrack"
dotnet run --project src/InfoTrack.Api/InfoTrack.Api.csproj
```

The API applies EF migrations on startup, so the database schema is created automatically on first run.

By default the API listens on **http://localhost:5194** (see `src/InfoTrack.Api/Properties/launchSettings.json`).

In Development, Scalar is available at http://localhost:5194/scalar/v1.

### Quick smoke test (local)

```bash
curl http://localhost:5194/health
curl http://localhost:5194/api/locations
```

Example search (hits the live site — may take 30–60 seconds for multiple cities):

```bash
curl -X POST http://localhost:5194/api/searches \
  -H "Content-Type: application/json" \
  -d '{"locations":["London","Leeds"]}'
```

Replace port `5194` with `8080` when testing against the Docker container.

More request examples are in `src/InfoTrack.Api/InfoTrack.Api.http`.

### Run the SPA in dev mode

The SPA uses Vite's dev server with a proxy so API calls are forwarded to the locally running API:

```bash
cd web
npm install   # first time only
npm run dev
```

The Vite dev server starts on **http://localhost:5173**. It proxies `/api` and `/health` requests to `http://localhost:5194`, so the API must already be running (see "Run the API" above).

To build the SPA into the API's `wwwroot` for a local production-like test:

```bash
cd web
npm run build   # outputs to ../src/InfoTrack.Api/wwwroot
```

Then run the API normally — it will serve the compiled SPA at its root.

## API endpoints

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/health` | Liveness check |
| `GET` | `/api/locations` | Default location list from configuration |
| `POST` | `/api/searches` | Scrape locations; returns results, report, and `runId` |
| `GET` | `/api/searches` | List all past runs, newest first |
| `GET` | `/api/searches/{id}` | Re-open a stored run with its recomputed report |
| `GET` | `/api/searches/{id}/changes` | Per-location change view with `Provisional`/`Confirmed` confidence |
| `GET` | `/api/firms` | Current-firms projection; filter by `status` or `addedSince` |
| `GET` | `/api/firms/{id}` | Single firm's current state and review-count history |
| `GET` | `/api/firms/{id}/history` | Review-count history and overall trend for a firm |

Full request/response shapes, comparability values, confidence semantics, and shared types are in [docs/API-reference.md](docs/API-reference.md).

## Configuration

Settings are in `src/InfoTrack.Api/appsettings.json`. Override any value with environment variables using the `__` separator (ASP.NET Core convention).

| Key | Purpose | Default |
|-----|---------|----------|
| `Scraper:BaseUrl` | solicitors.com base URL | `https://www.solicitors.com` |
| `Scraper:UserAgent` | HTTP User-Agent on scrape requests | `Mozilla/5.0 (compatible; InfoTrackBot/1.0; ...)` |
| `Scraper:TimeoutSeconds` | Per-request timeout | `15` |
| `Search:MaxParallelism` | Concurrent location fetches | `4` |
| `Scraper:DefaultLocations` | Cities returned by `GET /api/locations` | 8 UK cities (see `appsettings.json`) |
| `ChangeDetection:ConfirmationWindow` | Consecutive successful runs required to confirm a change | `3` |
| `ConnectionStrings:Postgres` | Postgres connection string | _(not set — required)_ |

The connection string key is `ConnectionStrings:Postgres`. When running via Docker Compose the value is injected as the environment variable `ConnectionStrings__Postgres` (double-underscore notation). To override locally:

```bash
# PowerShell
$env:ConnectionStrings__Postgres = "Host=localhost;Port=5432;Database=infotrack;Username=infotrack;Password=infotrack"
```

## Database schema

`db/schema.sql` contains an idempotent SQL script that reproduces the full schema. It is regenerated whenever migrations change:

```bash
dotnet ef migrations script --idempotent \
  --project src/InfoTrack.Infrastructure \
  --startup-project src/InfoTrack.Api \
  --output db/schema.sql
```

The schema is applied automatically on startup via `MigrateAsync`. There is no need to run `db/schema.sql` manually in normal operation — it exists as a reference and for environments where running EF migrations directly is not possible.

## Architecture

Four projects, dependencies point inward:

```
Api  →  Application  →  Domain
         ↑
    Infrastructure
```

- **Domain** — records and enums only
- **Application** — use-case services and port interfaces
- **Infrastructure** — HTTP fetcher, hand-written HTML parser, location resolver, EF Core repository
- **Api** — composition root, Minimal API endpoints

HTML parsing uses only the .NET BCL (`Regex`, `WebUtility.HtmlDecode`, string scanning). No HtmlAgilityPack, AngleSharp, Selenium, or Playwright.

EF Core and Npgsql are confined to `Infrastructure`. `Domain` and `Application` have no EF dependency.

## Design decisions and trade-offs

### Change-detection contract

`GET /api/searches/{id}/changes` compares each location against the most recent earlier successful run **for that specific location**, so each city always uses its own nearest baseline rather than a shared global run.

The target site rotates which firms it surfaces per scrape, so a single absence is not treated as a confirmed departure. Each reported change carries a `Provisional` or `Confirmed` confidence. **Confirmed** requires the change to hold across `K` consecutive successful runs (`ChangeDetection:ConfirmationWindow`, default `3`); **Provisional** means the change was observed but not yet sustained across the full window. A reappearance at any point in the window resets the counter.

A change claim is only made when the subject run's scrape returned `Success` for that location. Any other outcome is labelled `ScrapeFailed` with empty firm lists, preventing a scrape failure from appearing as a mass disappearance.

Absence is recorded as "not seen in this run" — there is no `deleted` flag on a firm.

### Latest-attributes trade-off

`FirmEntity` stores the *latest observed* attributes for a firm (name, address, phone, etc.), refreshed on every save that includes that firm. `ReviewCount` is the exception — it is stored per sighting because it drives ranking and legitimately changes between runs.

As a consequence, re-reading an old run returns that run's review counts but the firm's *current* address and contact details. Per-run attribute history is not stored; this is the deliberate duplication we avoid. The diff is unaffected because it compares branch identity (stable) and per-sighting review counts.

### Concurrent saves

The `IdentityKey` unique index on `FirmEntity` is the integrity backstop for concurrent inserts of the same firm. For the MVP no retry-on-conflict logic is implemented — a demo issues one save per POST. Under concurrent load a unique-constraint violation is possible; this is a known limitation.

## Known data caveats

- **No email addresses** on listing pages. The "Email" link points to an on-site enquiry form (`enquiry-form.asp`); we capture that as `enquiryUrl`.
- **Review count, not star rating.** The `(330)` next to a firm name is the number of reviews, not a score.
- **De-duplication** uses normalised firm name + postcode (phone as fallback). The same chain in two cities counts once in `uniqueSolicitors` but appears in the `multiLocationFirms` report section.


## Project layout

```
src/
  InfoTrack.Domain/          Models
  InfoTrack.Application/     Services and ports
  InfoTrack.Infrastructure/  Fetcher, parser, resolver, EF repository
  InfoTrack.Api/             API host
tests/
  InfoTrack.Tests/           xUnit tests and HTML fixtures
db/
  schema.sql                 Idempotent Postgres schema script
Dockerfile
docker-compose.yml
```
