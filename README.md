# InfoTrack Solicitor Intelligence Tool

A .NET API that scrapes conveyancing solicitor listings from [solicitors.com](https://www.solicitors.com), de-duplicates firms across locations, and returns a structured report with sales insights (top firms by review count, multi-location chains, coverage gaps, contactability).

Runs are on-demand. Persistence and change detection are planned for Phase 2.

## Prerequisites

| Tool | Version |
|------|---------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.x |
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | only for the Docker workflow |

Outbound HTTPS to `www.solicitors.com` is required when calling `POST /api/searches` (parser tests run offline against saved HTML fixtures).

## Run with Docker

From the repository root:

```bash
docker compose up --build
```

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

Rebuild after code changes:

```bash
docker compose up --build
```

The Compose file defines a single `api` service. A Postgres `db` service will be added in Phase 2.

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

Parser tests load embedded HTML fixtures from `tests/InfoTrack.Tests/Fixtures/` and do not call the live site.

### Run the API

```bash
dotnet run --project src/InfoTrack.Api/InfoTrack.Api.csproj
```

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
| `POST` | `/api/searches` | Scrape one or more locations; returns results and report |

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

Example override:

```bash
Scraper__MaxParallelism=2 dotnet run --project src/InfoTrack.Api/InfoTrack.Api.csproj
```

## Architecture

Four projects, dependencies point inward:

```
Api  →  Application  →  Domain
         ↑
    Infrastructure
```

- **Domain** — records and enums only
- **Application** — use-case services and port interfaces
- **Infrastructure** — HTTP fetcher, hand-written HTML parser, location resolver
- **Api** — composition root, Minimal API endpoints

HTML parsing uses only the .NET BCL (`Regex`, `WebUtility.HtmlDecode`, string scanning). No HtmlAgilityPack, AngleSharp, Selenium, or Playwright.

## Known data caveats

- **No email addresses** on listing pages. The "Email" link points to an on-site enquiry form (`enquiry-form.asp`); we capture that as `enquiryUrl`.
- **Review count, not star rating.** The `(330)` next to a firm name is the number of reviews, not a score.
- **De-duplication** uses normalised firm name + postcode (phone as fallback). The same chain in two cities counts once in `uniqueSolicitors` but appears in the `multiLocationFirms` report section.
- **Bradford** is not in the site's "Key Locations" index but returns real results at `conveyancing+bradford.html` (verified in test fixtures).

## Project layout

```
src/
  InfoTrack.Domain/          Models
  InfoTrack.Application/     Services and ports
  InfoTrack.Infrastructure/  Fetcher, parser, resolver
  InfoTrack.Api/             API host
tests/
  InfoTrack.Tests/           xUnit tests and HTML fixtures
Dockerfile
docker-compose.yml
```
