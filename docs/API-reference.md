# InfoTrack API Reference

Base path: `/api`  
All responses use `application/json`. All timestamps are ISO 8601 UTC (e.g. `2026-06-21T14:30:45.123456Z`).

---

## Table of Contents

- [POST /api/searches](#post-apisearches)
- [GET /api/searches](#get-apisearches)
- [GET /api/searches/{id}](#get-apisearchesid)
- [GET /api/searches/{id}/changes](#get-apisearchesidchanges)
- [GET /api/firms](#get-apifirms)
- [GET /api/firms/{id}](#get-apifirmsid)
- [GET /api/firms/{id}/history](#get-apifirmsidhistory)
- [GET /api/locations](#get-apilocations)
- [GET /health](#get-health)
- [Shared Types](#shared-types)

---

## POST /api/searches

**Value**: The primary operation. Scrapes `solicitors.com` across one or more locations for conveyancing solicitors, deduplicates the results, builds an analytical report, and persists everything to the database. Returns immediately with the full result — the caller never has to poll.

### Request

**Body** (JSON):

```json
{
  "locations": ["London", "Manchester", "Birmingham"]
}
```

| Field | Type | Constraints |
|---|---|---|
| `locations` | `string[]` | At least one non-blank entry required |

Location values are trimmed, deduplicated (case-insensitive), and empty strings are dropped before processing.

### Response

**200 OK** — always returned on a valid request, even when individual locations fail to scrape:

```json
{
  "result": {
    "runAtUtc": "2026-06-21T14:30:45Z",
    "areaOfLaw": "Conveyancing",
    "locationOutcomes": [
      {
        "location": "London",
        "requestedUrl": "https://www.solicitors.com/conveyancing+london.html",
        "status": "Success",
        "solicitors": [ /* Solicitor[] */ ],
        "errorMessage": null
      }
    ],
    "uniqueSolicitors": [ /* Solicitor[] — cross-location deduplicated */ ]
  },
  "report": {
    "summary": {
      "totalLocationsRequested": 3,
      "successfulLocations": 2,
      "emptyLocations": 1,
      "unavailableLocations": 0,
      "errorLocations": 0,
      "totalUniqueSolicitors": 247,
      "runAtUtc": "2026-06-21T14:30:45Z"
    },
    "locationSummaries": [
      { "location": "London", "status": "Success", "solicitorCount": 120, "errorMessage": null }
    ],
    "topFirmsByReviewCount": [
      { "firmName": "Smith & Co", "location": "London", "reviewCount": 482 }
    ],
    "multiLocationFirms": [
      { "normalisedFirmName": "smith and co", "locations": ["London", "Manchester"], "locationCount": 2 }
    ],
    "contactability": {
      "totalFirms": 247,
      "withPhone": 210,
      "withWebsite": 185,
      "withPhoneOrWebsite": 230,
      "percentWithPhone": 85.0,
      "percentWithWebsite": 74.9,
      "percentWithPhoneOrWebsite": 93.1
    }
  },
  "runId": "550e8400-e29b-41d4-a716-446655440000"
}
```

`runId` is `null` if the database write failed — the scrape result is still fully returned (persistence is best-effort).

**422 Unprocessable Entity** — empty locations list:

```json
{
  "errors": { "locations": ["At least one location is required."] }
}
```

### How it works

1. **Location resolution** — each location string is normalised into a URL slug (e.g. `"London"` → `/conveyancing+london.html`).

2. **Parallel scraping** — all locations are fetched concurrently, bounded by `Search:MaxParallelism` (default: 4). Per-location errors are isolated: one location failing does not abort the run.  
   - HTTP GET to `{Scraper:BaseUrl}/conveyancing+{slug}.html` with a 15-second timeout.
   - 404 → `LocationOutcomeStatus.Unavailable`; non-2xx → `LocationOutcomeStatus.Error`; 0 results parsed → `LocationOutcomeStatus.Empty`.

3. **HTML parsing** — BCL-only regex and string scanning extracts firm name, address, postcode, phone, website, enquiry URL, profile URL, review count, description, logo, and listing tier (`Featured` or `Standard`). No third-party HTML libraries are used.

4. **Deduplication** — solicitors are deduplicated across all locations using the branch identity key `normalise(FirmName) | {Postcode or Phone}`. First occurrence wins.

5. **Report computation** — `ReportBuilder` aggregates the deduplicated solicitors into the `SearchReport` structure (top 10 by review count, multi-location firms, contactability percentages).

6. **Persistence** — the run and all sightings are written to PostgreSQL:
   - Inserts a `SearchRuns` row.
   - Upserts `Firms` rows (insert on first sight, update attributes on subsequent sights).
   - Inserts `LocationOutcomes` and `Sightings` rows (one `Sighting` per firm × location pairing).
   - Failure is logged; the response is returned regardless.

---

## GET /api/searches

**Value**: Lists all past search runs newest-first so callers can navigate run history and pick a run ID for deeper inspection.

### Request

No parameters.

### Response

**200 OK**:

```json
[
  {
    "runId": "550e8400-e29b-41d4-a716-446655440000",
    "runAtUtc": "2026-06-21T14:30:45Z",
    "areaOfLaw": "Conveyancing",
    "locationCount": 3,
    "totalUniqueFirms": 247
  }
]
```

Returns `[]` if no runs have been stored. Returns metadata only — no firms or report data.

### How it works

Single DB read: selects `Id`, `RunAtUtc`, `AreaOfLaw`, `TotalLocations`, and `TotalUniqueFirms` from the `SearchRuns` table ordered by `RunAtUtc DESC`. No joins.

---

## GET /api/searches/{id}

**Value**: Re-opens a stored run. The report is recomputed from the stored sightings on every read, so it always reflects the current report logic without requiring a re-scrape.

### Request

| Param | Type | Location |
|---|---|---|
| `id` | `Guid` | Route |

### Response

**200 OK** — same shape as `POST /api/searches`:

```json
{
  "result": { /* SearchResult */ },
  "report": { /* SearchReport */ },
  "runId": "550e8400-e29b-41d4-a716-446655440000"
}
```

**404 Not Found**:

```json
{
  "status": 404,
  "title": "Run not found.",
  "detail": "No search run with id 550e8400-e29b-41d4-a716-446655440000."
}
```

### How it works

1. Loads the run from `SearchRuns` with eager-loaded `LocationOutcomes → Sightings → Firms` in one query.
2. Reconstructs the `SearchResult`: rebuilds the `Solicitor` list for each location from the stored sightings (review count is the value captured at scrape time), then re-runs the cross-location deduplication.
3. Runs `ReportBuilder.Build(result)` to produce a fresh `SearchReport`.

No external HTTP calls are made.

---

## GET /api/searches/{id}/changes

**Value**: Shows what changed between the given run and the most recent earlier successful run of each location — which firms appeared, which disappeared — with a confidence rating so the caller can distinguish genuine changes from scraping noise.

### Request

| Param | Type | Location |
|---|---|---|
| `id` | `Guid` | Route — the "subject" run to compare |

### Response

**200 OK**:

```json
{
  "subjectRunId": "550e8400-e29b-41d4-a716-446655440000",
  "subjectRunAtUtc": "2026-06-21T14:30:45Z",
  "locations": [
    {
      "location": "London",
      "comparability": "Comparable",
      "baselineRunId": "66077ea2-dcd4-4abd-a0fa-0e4a5b39c72a",
      "newFirms": [
        { "firm": { /* Solicitor */ }, "confidence": "Confirmed" }
      ],
      "absentFirms": [
        { "firm": { /* Solicitor */ }, "confidence": "Provisional" }
      ]
    },
    {
      "location": "Manchester",
      "comparability": "NoBaseline",
      "baselineRunId": null,
      "newFirms": [],
      "absentFirms": []
    }
  ]
}
```

`comparability` values:

| Value | Meaning |
|---|---|
| `Comparable` | Location scraped successfully and a baseline run exists; diff was computed |
| `ScrapeFailed` | Location failed or was empty in the subject run — no diff possible |
| `NoBaseline` | Location scraped successfully but no earlier successful run exists for comparison |
| `NotRequested` | Location was not included in the subject run |

`confidence` values:

| Value | Meaning |
|---|---|
| `Confirmed` | The change appears consistently across the last K runs; very likely genuine |
| `Provisional` | The change appeared in only some runs; may be a scraping artefact or transient state |

**404 Not Found** — subject run not found.

### How it works

`LocationChangeService` processes each location in the subject run:

1. If the location's scrape failed: emits `ScrapeFailed`.
2. If scraping succeeded: queries the `K+1` most recent successful runs of that location up to and including the subject run (`K` = `ChangeDetection:ConfirmationWindow`, default: 3).
   - Fewer than 2 results → `NoBaseline`.
   - Two or more results: the most recent is the subject, the next is the baseline. Firms are diffed by their branch identity key.
3. **Confidence for a new firm** — `Confirmed` if the firm was absent from all K prior runs (indices 1..K); `Provisional` if it appeared in any of them (indicates possible under-sampling recovery rather than a genuine arrival).
4. **Confidence for an absent firm** — `Confirmed` if the firm is missing from all K runs including the subject (indices 0..K-1); `Provisional` if it reappeared in any of them (indicates possible scraping flicker rather than a genuine departure).

The per-location design means each location is compared to its own most recent baseline, not to a global previous run. This handles the case where locations are not always included together.

Database: reads `SearchRuns`, `LocationOutcomes`, `Sightings`, and `Firms`. No external HTTP calls.

---

## GET /api/firms

**Value**: Projects the complete current state of every known firm — whether it is still active, appears to have left, or has been confirmed gone — across all locations where it has ever appeared. Supports filtering by status and first-seen date for monitoring workflows.

### Request

| Param | Type | Location | Description |
|---|---|---|---|
| `status` | `string` | Query, optional | `active`, `provisional`, or `gone` |
| `addedSince` | `DateTimeOffset` | Query, optional | ISO 8601 datetime; returns only firms first seen at or after this value |

Examples:
- `/api/firms` — all firms
- `/api/firms?status=active`
- `/api/firms?status=provisional&addedSince=2026-06-01T00:00:00Z`

### Response

**200 OK**:

```json
[
  {
    "firmId": "550e8400-e29b-41d4-a716-446655440000",
    "latest": { /* Solicitor — most recent scraped attributes */ },
    "firstSeenAt": "2026-06-01T10:00:00Z",
    "lastSeenAt": "2026-06-21T14:30:00Z",
    "rollupStatus": "Active",
    "locations": [
      { "location": "London", "status": "Active", "lastSeenAt": "2026-06-21T14:30:00Z" },
      { "location": "Manchester", "status": "ProvisionallyAbsent", "lastSeenAt": "2026-06-18T09:00:00Z" }
    ]
  }
]
```

`rollupStatus` is the best (most alive) status across all of the firm's locations. A firm active in one location and provisionally absent in another has `rollupStatus: "Active"`.

**422 Unprocessable Entity** — unrecognised `status` value:

```json
{
  "errors": { "status": ["Unknown status 'xyz'. Valid values: active, provisional, gone."] }
}
```

### How it works

`CurrentFirmsProjector` builds the projection in two DB reads (avoiding N×M fan-out):

1. Loads the K most recent successful runs per location (`K` = `ChangeDetection:ConfirmationWindow`).
2. Loads the most-recently-seen attributes for every firm across every location.

For each location, firms present in the latest run are marked `Active`. Firms absent from the latest run have their confidence computed using the same `ChangeConfirmer` algorithm as the changes endpoint:
- Absent from all K recent runs → `ConfirmedGone`
- Absent from only some → `ProvisionallyAbsent`

Filtering by `status` and `addedSince` is applied in-memory after the projection. No external HTTP calls.

---

## GET /api/firms/{id}

**Value**: Full profile of a single firm: its current state across all locations plus a chronological review-count history with an overall trend. Useful for monitoring a specific firm's trajectory over time.

### Request

| Param | Type | Location |
|---|---|---|
| `id` | `Guid` | Route |

### Response

**200 OK**:

```json
{
  "firm": {
    "firmId": "550e8400-e29b-41d4-a716-446655440000",
    "latest": { /* Solicitor */ },
    "firstSeenAt": "2026-06-01T10:00:00Z",
    "lastSeenAt": "2026-06-21T14:30:00Z",
    "rollupStatus": "Active",
    "locations": [ /* FirmLocationState[] */ ]
  },
  "history": {
    "firmId": "550e8400-e29b-41d4-a716-446655440000",
    "points": [
      { "runAtUtc": "2026-06-15T10:00:00Z", "location": "London", "reviewCount": 42 },
      { "runAtUtc": "2026-06-21T14:30:00Z", "location": "London", "reviewCount": 45 }
    ],
    "overallReviewTrend": "Rising"
  }
}
```

`overallReviewTrend` values: `Rising`, `Falling`, `Steady`, `Unknown` (fewer than two data points with a non-null review count).

**404 Not Found** — firm not found.

### How it works

Two parallel operations:

- `CurrentFirmsProjector.BuildForFirmAsync` — uses the same projection logic as `GET /api/firms` but scoped to a single firm. If the firm has no sightings, returns `null` → 404.
- `ReviewTrendService.BuildAsync` — queries all sightings for this firm ordered by `RunAtUtc` ascending, then compares the first and last non-null review count values.

No external HTTP calls.

---

## GET /api/firms/{id}/history

**Value**: The review-count trend for a single firm without the full current-state projection. Lighter than `GET /api/firms/{id}` when only the history is needed.

### Request

| Param | Type | Location |
|---|---|---|
| `id` | `Guid` | Route |

### Response

**200 OK**:

```json
{
  "firmId": "550e8400-e29b-41d4-a716-446655440000",
  "points": [
    { "runAtUtc": "2026-06-15T10:00:00Z", "location": "London", "reviewCount": 42 },
    { "runAtUtc": "2026-06-18T09:15:00Z", "location": "Manchester", "reviewCount": 38 },
    { "runAtUtc": "2026-06-21T14:30:00Z", "location": "London", "reviewCount": 45 }
  ],
  "overallReviewTrend": "Rising"
}
```

**404 Not Found** — no sightings found for this firm ID (zero `points`).

### How it works

Single DB read via `ISightingRepository.GetFirmReviewHistoryAsync`: joins `Sightings → LocationOutcomes → SearchRuns` to collect `(RunAtUtc, Location, ReviewCount)` ordered ascending. Trend is classified by comparing the first and last non-null review count values. No external HTTP calls.

---

## GET /api/locations

**Value**: Returns the pre-configured default locations that can be passed directly to `POST /api/searches`. Useful for populating a UI location picker without hardcoding values in the client.

### Request

No parameters.

### Response

**200 OK**:

```json
["London", "Manchester", "Birmingham", "Leeds", "Sheffield", "Bristol", "Liverpool", "Nottingham"]
```

Returns the `Scraper:DefaultLocations` array from application configuration. No DB access, no external HTTP calls.

---

## GET /health

**Value**: Liveness check. Returns 200 if the API process is running and able to handle requests.

### Request

No parameters.

### Response

**200 OK**:

```json
{ "status": "healthy" }
```

No DB access, no external HTTP calls. Does not verify database connectivity.

---

## Shared Types

### Solicitor

Represents a single firm listing as scraped from one location.

```json
{
  "firmName": "Smith & Co Solicitors",
  "searchedLocation": "London",
  "address": "12 High Street",
  "town": "London",
  "postcode": "EC1A 1BB",
  "phone": "020 7123 4567",
  "websiteUrl": "https://smithandco.example",
  "enquiryUrl": "https://www.solicitors.com/enquiry/...",
  "profileUrl": "https://www.solicitors.com/firms/smith-and-co",
  "reviewCount": 482,
  "description": "Specialist conveyancing practice...",
  "logoUrl": "https://www.solicitors.com/logos/smith-and-co.png",
  "tier": "Featured",
  "scrapedAtUtc": "2026-06-21T14:30:45Z"
}
```

| Field | Notes |
|---|---|
| `tier` | `Featured` — full card with logo, enquiry link, website link; `Standard` — compact row with phone only |
| `reviewCount` | Nullable; absent for firms with no reviews |
| `enquiryUrl` | Points to the solicitors.com enquiry form, not the firm's own site |
| `searchedLocation` | The location string supplied in the original request |

### LocationOutcomeStatus

| Value | Meaning |
|---|---|
| `Success` | Page fetched and ≥1 firm parsed |
| `Empty` | Page fetched successfully but 0 firms found |
| `Unavailable` | HTTP 404 — location not listed on solicitors.com |
| `Error` | Any other HTTP error or unhandled parsing exception |

---

## Configuration Reference

Key values that affect API behaviour (set in `appsettings.json` or via environment variables with `__` as separator):

| Key | Default | Effect |
|---|---|---|
| `Scraper:BaseUrl` | `https://www.solicitors.com` | Root URL for all scrape requests |
| `Scraper:TimeoutSeconds` | `15` | HTTP request timeout per location |
| `Scraper:DefaultLocations` | 8 English cities | Returned by `GET /api/locations` |
| `Search:MaxParallelism` | `4` | Max concurrent location fetches per search |
| `ChangeDetection:ConfirmationWindow` | `3` | Number of prior runs (K) required to confirm a change |
| `ConnectionStrings:Postgres` | — | PostgreSQL connection string |

The `ConfirmationWindow` (K) affects the changes endpoint and the firms projection: a firm must be absent from K consecutive runs before its absence is rated `Confirmed`. Raising K reduces false positives but requires more historical data before confidence is assigned.
