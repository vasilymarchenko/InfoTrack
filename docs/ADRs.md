# Architecture Decision Records — InfoTrack

> Format: each ADR records the **context** (forces at play), the **decision** (what was chosen and why), and the **consequences** (trade-offs and things to watch). Status is one of: *Accepted* · *Superseded* · *Deprecated*.

---

## ADR-001 — Ports-and-adapters layered architecture

**Status:** Accepted  
**Phase:** 1

### Context

The primary purpose of this project is to demonstrate how to structure a solution clearly. The risk with a flat or anemic design is that HTTP concerns, parsing logic, and business rules blur together, making the code hard to read, test, or extend.

### Decision

Adopt a four-layer ports-and-adapters structure with strictly inward-pointing dependencies:

```
Api  →  Application  →  Domain
         ↑
    Infrastructure
```

- **Domain** — plain records and enums; zero external dependencies.
- **Application** — use-case services and port *interfaces* (`IListingFetcher`, `IListingParser`, `ILocationResolver`, `IReportBuilder`, `ISearchRunRepository`). Depends only on Domain.
- **Infrastructure** — adapters that implement the ports (HTTP fetcher, HTML parser, EF repository). Depends on Application + Domain.
- **Api** — composition root (Minimal APIs, DI wiring, OpenAPI). The only layer that references Infrastructure.

The dependency direction is enforced via `<ProjectReference>` entries in the `.csproj` files. Domain must never reference Infrastructure; Application must never reference Infrastructure.

### Consequences

- Each layer can be tested in isolation: Domain and Application against pure in-memory fakes; Infrastructure against saved HTML fixtures and an in-process database.
- Adding a new adapter (e.g. a second scraping target, or swapping Postgres for SQLite) requires only a new Infrastructure class and a DI change in Api.
- More projects than a flat structure, which is a cost worth paying given the explicit requirement to show how the code is organised.

---

## ADR-002 — No third-party HTML parsing library

**Status:** Accepted  
**Phase:** 1

### Context

Several mature libraries exist for parsing HTML in .NET: HtmlAgilityPack, AngleSharp, and browser-automation stacks such as Selenium and Playwright. Using one would reduce the amount of parsing code written from scratch.

The task specification explicitly asks to see how the code is structured. A library that hides the parsing mechanism would obscure the part of the implementation under evaluation.

### Decision

Use only the .NET BCL for all HTML extraction:

- `System.Text.RegularExpressions` for pattern matching.
- `System.Net.WebUtility.HtmlDecode` for entity decoding.
- String scanning and indexing for structural navigation.

No HtmlAgilityPack, AngleSharp, Selenium, Playwright, or any other HTML/DOM library may appear in any `.csproj` file. This is a verifiable constraint — CI can grep for forbidden package references.

### Consequences

- The parser must handle edge cases in the target HTML defensively (missing elements, banner siblings, commented-out markup).
- Parsing logic is fully visible, testable, and isolated — no hidden library behaviour.
- Any structural change to the solicitors.com page template requires a code change. This is accepted because the target is a single known site, not arbitrary HTML.
- This constraint directly motivated the parser rework documented in ADR-005.

---

## ADR-003 — Fixture-first parser development and offline testing

**Status:** Accepted  
**Phase:** 1

### Context

Parser logic derived from assumptions about HTML structure is fragile. The target page is a live commercial site whose markup cannot be reliably inferred from documentation or inspection of a single element.

Tests that hit the live site are non-deterministic, slow, and break on network or site changes.

### Decision

Before writing any parsing code, capture real HTML responses from the target site and save them as static fixtures under `tests/InfoTrack.Tests/Fixtures/`. The parser is written against these fixtures, and all parser tests load them from disk.

Rules:
- Fixtures are committed to source control alongside the tests.
- No parser test may make a real network call.
- The zero-results fixture must use a slug that is genuinely non-existent (e.g. `conveyancing+fakecityxyz.html`), not a location that happens to return results.
- Fixture-derived counts (e.g. Birmingham = 75 firms, Bradford = 23 firms) are treated as locked baselines. Any change to those counts requires explicit review.

### Consequences

- Tests run offline and produce deterministic results in CI with no external dependencies.
- When the solicitors.com page template changes, tests fail at the fixture level, giving a clear signal to update both the fixture and the parser.
- Fixtures must be periodically refreshed to remain representative of the live site.

---

## ADR-004 — Fetch with `HttpClient`; no headless browser

**Status:** Accepted  
**Phase:** 1

### Context

The solicitors.com listing pages display a "JavaScript must be enabled" banner. This might suggest the content is rendered client-side and requires a headless browser (Selenium, Playwright) to retrieve.

Inspection of the raw HTTP response confirms otherwise.

### Decision

Use a plain `HttpClient` GET to retrieve listing pages. Despite the JavaScript banner, the solicitor data is present in the server-rendered HTML response and does not require JavaScript execution.

Configuration:

- A descriptive `User-Agent` is set on the `HttpClient` to identify the scraper honestly.
- A configurable request timeout (default 15 seconds) prevents indefinite hangs.
- HTTP 404 and redirect-to-non-results responses are mapped to `FetchResult.NotFound` — they must not throw.
- Other failures return `FetchResult.Error(message)` and are caught per-location (see ADR-009).
- Concurrency is bounded to avoid hammering the target host (see ADR-008).

No Selenium, Playwright, or browser-automation dependency is permitted.

### Consequences

- Fetching is fast, lightweight, and testable via a stubbed `HttpMessageHandler`.
- If solicitors.com moves to client-side rendering, the fetcher will return markup with no data and the parser will produce zero results — this is detectable and the architecture (behind a port) allows swapping in a headless-browser adapter without touching Application or Domain.

---

## ADR-005 — Depth-aware block segmenter for HTML parsing

**Status:** Accepted  
**Phase:** 1

### Context

solicitors.com lists each firm in a `div.result-item` inside `div.result-section`. Ad banners (`div.banner-block`) can appear as siblings between firms. Slicing from one `result-item` opening tag to the next includes any intervening siblings in the preceding block, which lets downstream extractors — notably `ContactLinkClassifier` — pick up a banner's external URL when a firm has no website of its own.

Under ADR-002, parsing uses BCL string scanning only. There is no DOM tree; block boundaries must be found without a general HTML parser.

### Decision

`SolicitorsComConveyancingParser` (`IListingParser`) is a thin orchestrator over internal components in `Parsing.SolicitorsCom`. Stages pass plain `string` slices — no intermediate DOM.

```
html
  → ResultSectionLocator          (result-section region; comments stripped)
  → ResultItemSegmenter           (one balanced result-item per firm)
  → ResultItemParser              (block → Solicitor?)
       HtmlScanner                 (Attr / LeadingText / InnerText / Section)
       ContactLinkClassifier       (website vs enquiry, scoped to ul.list-item)
       AddressParser               (address text → Town?, Postcode?)
       HtmlText                    (HtmlDecode, whitespace collapse, Absolutise)
```

`ResultItemSegmenter` finds each `div.result-item` opening tag, then scans forward counting `+1` on every `<div` and `-1` on every `</div>`. When depth returns to zero, the element's closing tag has been reached. It yields `region[start..end]`; sibling banners and trailing whitespace are excluded.

`ResultSectionLocator` strips HTML comments before segmentation so a `<div` inside a comment cannot skew the depth counter.

`HtmlScanner.InnerText` and `HtmlScanner.Section` assume the target tag is not self-nested. That holds for every field on the current template. A future self-nesting field should use the same depth-balancing approach locally rather than generalising the scanner pre-emptively.

All sub-components are `internal`; tests access them via `InternalsVisibleTo("InfoTrack.Tests")`.

### Consequences

- Sibling elements never bleed into a firm's block; a region with a `banner-block` between two `result-item`s yields exactly two blocks, neither containing the banner.
- Each component has unit tests on inline HTML snippets; end-to-end tests use saved fixtures (Birmingham → 75 firms, Bradford → 23 firms).
- Parsing logic is visible, composable, and isolated — no third-party HTML library (ADR-002).
- Markup changes on solicitors.com require targeted updates to the relevant component, not a library upgrade.

---

## ADR-007 — Firm identity and de-duplication rule

**Status:** Accepted  
**Phase:** 1

### Context

The same firm can appear multiple times in a single location's results (multiple branches). National chains appear across many cities. Two distinct grouping concepts are needed:

1. **Branch identity** — is this the same physical office?
2. **Firm identity** — is this the same company, regardless of branch?

### Decision

**Branch identity** (used for `UniqueSolicitors` de-duplication):

```
key = normalise(FirmName) + Postcode
```

If `Postcode` is null, fall back to `Phone`. Two records with the same key are the same branch and one is discarded.

**Firm identity** (used for the multi-location insight):

```
key = normalise(FirmName)
```

Firms appearing in two or more locations under the same normalised name are classified as multi-location firms.

Normalisation: trim, lowercase, collapse internal whitespace.

This same rule governs `RunComparer` in Phase 2: new/dropped firms are identified by branch identity per location.

### Consequences

- A firm that moves premises (Postcode changes) will appear as a new firm in the diff — acceptable given the data available.
- A firm with no postcode and no phone cannot be de-duplicated with confidence. This edge case is accepted; the field is nullable and the fallback is best-effort.
- Any future change to the identity rule must be applied consistently in `SolicitorSearchService`, `ReportBuilder`, and `RunComparer`.

---

## ADR-008 — Bounded parallelism for concurrent location fetching

**Status:** Accepted  
**Phase:** 1

### Context

Fetching each location sequentially is unnecessarily slow. Fetching all locations in parallel without limit risks triggering rate-limiting or IP blocks on the target host, and consumes more local resources than necessary.

### Decision

Location fetches run concurrently, controlled by a `SemaphoreSlim` with a configurable maximum degree of parallelism (default: 4, configurable via `MaxParallelism` in `appsettings.json`).

```csharp
// Pseudocode
var semaphore = new SemaphoreSlim(options.MaxParallelism);
var tasks = locations.Select(async loc => {
    await semaphore.WaitAsync(ct);
    try { return await FetchAndParseAsync(loc, ct); }
    finally { semaphore.Release(); }
});
var outcomes = await Task.WhenAll(tasks);
```

A descriptive `User-Agent` header is set on all requests to identify the scraper honestly.

### Consequences

- Overall latency scales with `ceil(locationCount / MaxParallelism)` fetch round-trips rather than `locationCount`.
- The parallelism ceiling can be lowered in production if rate-limiting is observed, without a code change.
- CancellationToken is plumbed through all async calls so the entire run can be cancelled cleanly.

---

## ADR-009 — Per-location resilience: one failure must not abort the run

**Status:** Accepted  
**Phase:** 1

### Context

A multi-location search run is a best-effort operation. A single location returning 404, timing out, or throwing an unexpected exception must not cause the entire response to fail with a 500. The caller needs to know which locations succeeded and which did not.

### Decision

`SolicitorSearchService` wraps each location's fetch-and-parse pipeline in an independent `try/catch`. Exceptions and error signals are mapped to a `LocationOutcome` with an appropriate `LocationOutcomeStatus`:

| Condition | Status |
|-----------|--------|
| Parse returns ≥ 1 firm | `Success` |
| Parse returns 0 firms | `Empty` |
| Fetch returns `NotFound` | `Unavailable` |
| Any exception | `Error` (message recorded) |

The overall `SearchResponse` is always returned. The per-location status is included in the response so the caller can distinguish partial success from total failure.

### Consequences

- A misconfigured or unsupported location (or a transient network error) is surfaced in the response metadata, not as an HTTP error.
- The `ErrorMessage` field on `LocationOutcome` must not leak internal exception details to the API caller in production (log the full exception; return a sanitised message).
- Tests must assert that a faulting location does not prevent other locations in the same run from being processed.

---

## ADR-010 — EF Core entities are separate from Domain models

**Status:** Accepted  
**Phase:** 2

### Context

Entity Framework Core can use plain domain classes as entities directly, which reduces mapping code. However, doing so couples the Domain layer to EF-specific concerns: navigation properties, required parameterless constructors, column attributes, and the EF change-tracker lifecycle.

The layering rules in ADR-001 prohibit Infrastructure concerns from leaking into Domain or Application.

### Decision

Infrastructure defines its own set of EF entity classes (`SearchRunEntity`, `LocationOutcomeEntity`, `SolicitorEntity`) that mirror the Domain models structurally but are separate types. Mapping between Domain models and EF entities is done with small, explicit static mapping methods co-located with `AppDbContext` or the repository — no AutoMapper or code-generation tool.

Domain records (`Solicitor`, `LocationOutcome`, `SearchResult`) remain plain C# records with no EF attributes, no navigation properties, and no dependency on `Microsoft.EntityFrameworkCore`.

### Consequences

- Adding a field to a Domain model requires a corresponding change to the EF entity and the mapping method — a deliberate, visible cost that keeps the two representations in sync.
- The Domain and Application layers can be compiled and tested with no EF packages installed.
- If EF Core is replaced in the future, only Infrastructure is affected.

---

## ADR-011 — Persistence failure does not fail the search request

**Status:** Accepted  
**Phase:** 2

### Context

In Phase 2, `SolicitorSearchService.SearchAsync` calls `ISearchRunRepository.SaveAsync` after a successful scrape. If the database is unavailable or the save throws, there are two possible behaviours:

1. Propagate the exception → return HTTP 500 to the caller (the scrape is lost).
2. Log the error and return the scraped result anyway (the scrape is not persisted but the caller receives their data).

### Decision

If `SaveAsync` throws, the exception is caught, logged at error level, and the `SearchResponse` (with scrape results and report) is returned to the caller without a `RunId`. The caller receives their data; persistence is best-effort.

The response shape must accommodate a nullable `RunId` to signal when persistence was skipped.

This decision is recorded explicitly because both behaviours are defensible. The chosen behaviour prioritises availability of the scrape result over guaranteed persistence. If guaranteed persistence is required in the future (e.g. for audit purposes), this decision should be revisited.

### Consequences

- Callers must not assume a `RunId` is always present in the response.
- A failed save must produce a visible error log entry so the gap in history is discoverable.
- The diff and history endpoints will simply not include runs that failed to persist — there is no mechanism to retroactively recover them.

---

## ADR-012 — Report is recomputed on read, not stored in full

**Status:** Accepted  
**Phase:** 2

### Context

The `SearchReport` (containing run summary, per-location summaries, multi-location firms, top rankings, coverage gaps, contactability) is computed by `ReportBuilder` from the scraped data. It can be recomputed deterministically from `SearchResult` at any time.

Persisting the full report means adding a complex JSON column or additional tables, and requires a migration whenever the report structure changes.

### Decision

The full `SearchReport` is not persisted. `SearchRunEntity` stores only lightweight summary totals (total firms, unique firms, location count) needed for the `GET /api/searches` list view. When `GET /api/searches/{id}` is called, the full `SearchResult` is loaded from the database and `IReportBuilder.Build` is called again to recompute the report.

### Consequences

- Report schema can evolve (new insights added) without a database migration.
- Historical runs will reflect the *current* report logic when re-read, not the logic that was active at scrape time. If report definitions must be stable over time (e.g. for audit), this decision must be revisited.
- `ReportBuilder` must remain a pure function with no I/O, so recomputation is fast.

---

## ADR-013 — EF Core migrations are applied on startup

**Status:** Accepted  
**Phase:** 2

### Context

Database schema migrations can be applied in several ways: manually by a DBA before deployment, via a separate migration job in the deployment pipeline, or automatically when the application starts.

The goal for this project is `docker compose up` with zero additional steps.

### Decision

On application startup, `AppDbContext.Database.MigrateAsync()` is called before the API begins accepting requests. This applies any pending EF Core migrations automatically.

The call is guarded so it does not execute when the application is running with a non-relational provider (e.g. SQLite in-memory for unit tests).

A `db/schema.sql` script generated from the migrations (`dotnet ef migrations script --idempotent`) is committed to the repository as the canonical schema reference and submission artefact.

### Consequences

- `docker compose up` is fully self-contained: Postgres starts, the API waits for the healthcheck, then migrates and serves — no manual steps.
- In environments with multiple API instances starting simultaneously, migration races are possible. Acceptable for a single-instance development/demo deployment; would require a migration job or distributed lock in a multi-instance production setup.
- The startup migration must be removed or gated if the application is ever deployed to a managed environment where schema changes require DBA approval.

---

## ADR-014 — Multi-stage Docker build; single `api` service in Phase 1 Compose

**Status:** Accepted  
**Phase:** 1 (extended in Phase 2)

### Context

Docker images that include the full SDK are large and expose build tooling in production. The `docker-compose.yml` structure should reflect the current phase's actual service dependencies.

### Decision

The `Dockerfile` uses a **multi-stage build**:

1. SDK image (`mcr.microsoft.com/dotnet/sdk:10.0`) — restore, build, publish.
2. ASP.NET runtime image (`mcr.microsoft.com/dotnet/aspnet:10.0`) — copy published output, set entry point.

The Phase 1 `docker-compose.yml` defines a single `api` service. The file is structured to make a `db` service addition in Phase 2 a minimal diff. When Phase 2 adds Postgres, `api` gains a `depends_on` with `condition: service_healthy` pointing at the `db` service.

### Consequences

- The production image contains only the runtime and published output — no SDK, no source code.
- The single-service Phase 1 Compose is intentional; it is not an oversight to be "completed" before Phase 2.
- Port mapping (e.g. `8080:8080`) and `ASPNETCORE_*` environment variables are documented in the README.

---

## ADR-015 — Configuration via `appsettings.json` with environment variable override

**Status:** Accepted  
**Phase:** 2

### Context

The application has several runtime-variable settings: the scraping base URL, user agent, request timeout, maximum parallelism, default locations, and (in Phase 2) the database connection string. These must be settable without recompiling and without the value appearing in source control.

### Decision

All settings are bound from `appsettings.json` using standard .NET options pattern. Environment variables use the `__` double-underscore separator to override any setting, following ASP.NET Core's built-in configuration provider hierarchy.

The database connection string specifically uses:

- Config key: `ConnectionStrings:Postgres`
- Environment variable override: `ConnectionStrings__Postgres`

`docker-compose.yml` sets `ConnectionStrings__Postgres` on the `api` service to point at the `db` service hostname. No connection string or secret appears in committed files.

### Consequences

- `appsettings.json` contains safe development defaults (localhost connection strings, reasonable timeouts). Production values are injected via environment variables.
- The README documents the config key names, the environment variable names, and where each is set in the Compose file.
- Sensitive values (database passwords) appear only in the Compose file or in a `.env` file excluded from source control, never in `appsettings.json`.
