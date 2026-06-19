# InfoTrack Solicitor Intelligence Tool

A .NET API that scrapes conveyancing solicitor listings from [solicitors.com](https://www.solicitors.com), de-duplicates firms across locations, and returns a structured report with sales insights (top firms by review count, multi-location chains, coverage gaps, contactability). Search runs are persisted to Postgres; subsequent calls can retrieve stored results and compare runs to detect firm changes.

## Prerequisites

| Tool | Version |
|------|---------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.x |
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | required for the Docker workflow |

Outbound HTTPS to `www.solicitors.com` is required when calling `POST /api/searches` (parser tests run offline against saved HTML fixtures).

## Run with Docker

From the repository root:

```bash
docker compose up --build
```

`docker compose up` starts two services:

1. **`db`** — Postgres 17. Data is stored in a named Docker volume (`pgdata`), so it survives container restarts.
2. **`api`** — the .NET API. It waits for the `db` healthcheck to pass, then applies any pending EF migrations automatically on startup. No manual database setup is required.

The API listens on **http://localhost:8080**.

| URL | Purpose |
|-----|---------|
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

Parser tests load embedded HTML fixtures from `tests/InfoTrack.Tests/Fixtures/` and do not call the live site. `RunComparer` tests are pure (no database required).

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

## API endpoints

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/health` | Returns `200` with `{ "status": "healthy" }` |
| `GET` | `/api/locations` | Default location list from configuration |
| `POST` | `/api/searches` | Scrape one or more locations; persists the run and returns results, report, and `runId` |
| `GET` | `/api/searches` | List all past runs, newest first |
| `GET` | `/api/searches/{id}` | Re-open a stored run with its recomputed report |
| `GET` | `/api/searches/{id}/diff` | Per-location diff vs a previous run |

### `POST /api/searches`

Request body:

```json
{
  "locations": ["London", "Birmingham", "Leeds"],
  "areaOfLaw": "Conveyancing"
}
```

- `locations` — required, at least one city name
- `areaOfLaw` — optional, defaults to `"Conveyancing"` (only area currently supported)

Response shape:

```json
{
  "runId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "result": {
    "runAtUtc": "2026-06-17T12:00:00+00:00",
    "areaOfLaw": "Conveyancing",
    "locationOutcomes": [ "..." ],
    "uniqueSolicitors": [ "..." ]
  },
  "report": {
    "summary": { "..." },
    "locationSummaries": [ "..." ],
    "topFirmsByReviewCount": [ "..." ],
    "multiLocationFirms": [ "..." ],
    "coverageGaps": [ "..." ],
    "contactability": { "..." }
  }
}
```

Each location is fetched independently. A single location failing (404, timeout, parse error) does not abort the whole run — its status is reported in `locationOutcomes` as `Success`, `Empty`, `Unavailable`, or `Error`.

Persistence failures return `500 ProblemDetails` — the response body is not returned when the save did not complete.

### `GET /api/searches`

Returns an array of run summaries, newest first:

```json
[
  {
    "runId": "3fa85f64-...",
    "runAtUtc": "2026-06-17T12:00:00+00:00",
    "areaOfLaw": "Conveyancing",
    "locationCount": 3,
    "totalUniqueFirms": 42
  }
]
```

### `GET /api/searches/{id}`

Re-reads a stored run and recomputes the report on the fly. Returns the same shape as `POST /api/searches`. Returns `404 ProblemDetails` if the run does not exist.

### `GET /api/searches/{id}/diff?against={guid}`

Compares the run `{id}` (subject) against a baseline run.

- If `against` is omitted, the baseline is automatically the most recent run with a timestamp strictly earlier than the subject.
- If no earlier run exists, returns `200` with an explanatory message and an empty locations list.
- Returns `404 ProblemDetails` if either run is not found.

Response shape:

```json
{
  "subjectRunId": "3fa85f64-...",
  "baselineRunId": "1a2b3c4d-...",
  "message": null,
  "locations": [
    {
      "location": "London",
      "comparability": "Comparable",
      "newFirms": [ "..." ],
      "absentFirms": [ "..." ]
    },
    {
      "location": "Leeds",
      "comparability": "ScrapeFailed",
      "newFirms": [],
      "absentFirms": []
    }
  ]
}
```

`comparability` values:

| Value | Meaning |
|-------|---------|
| `Comparable` | Both runs scraped this location successfully — `newFirms`/`absentFirms` are meaningful |
| `ScrapeFailed` | At least one run returned `Empty`, `Unavailable`, or `Error` for this location — no change claim is made |
| `NotRequested` | Location only appears in one of the two runs — no comparison is possible |

## Configuration

Settings are in `src/InfoTrack.Api/appsettings.json` under the `Scraper` section. Override any value with environment variables using the `__` separator (ASP.NET Core convention).

| Key | Purpose | Default |
|-----|---------|---------|
| `Scraper:BaseUrl` | solicitors.com base URL | `https://www.solicitors.com` |
| `Scraper:UserAgent` | HTTP User-Agent on scrape requests | `InfoTrackBot/1.0` |
| `Scraper:TimeoutSeconds` | Per-request timeout | `15` |
| `Scraper:MaxParallelism` | Concurrent location fetches | `4` |
| `Scraper:DefaultLocations` | Cities returned by `GET /api/locations` | 8 UK cities (see `appsettings.json`) |
| `Scraper:CoverageGapThreshold` | Locations with firm count ≤ this appear in coverage gaps | `1` |
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

A change claim (`newFirms` / `absentFirms`) is made **only** when a location returned `Success` in **both** runs being compared. Any other combination is labelled `ScrapeFailed` or `NotRequested` with empty firm lists. This prevents a single scrape failure from appearing as a mass disappearance of firms.

Absence is recorded as "not seen in this run" — there is no boolean `deleted` or `active` flag on a firm. A firm that disappears and later reappears is simply new again in the run where it returns; no confirmation or debounce is applied (deferred to Phase 2 Full).

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
