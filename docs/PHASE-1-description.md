# Phase 1 — MVP Backend API (Description)

## Goal

Build the **minimum working backend** for the InfoTrack tool: an API that, given a list of
locations, scrapes conveyancing solicitors from `solicitors.com`, normalises the data, and
returns both the **raw results** and a **computed report / insights** payload.

No database, no history, no Entity Framework, and **no frontend** in this phase. The frontend
(a real SPA framework) is Phase 3; persistence is Phase 2.

## Why this scope

- It delivers the single most important capability in the task — turning a location list into
  usable solicitor data and insight — end to end.
- It keeps the moving parts small so the scraping/parsing logic (the part the task explicitly
  wants to "see how you structure") is the focus, not infrastructure.
- Everything added later (history, Postgres) plugs into clean seams defined here.

## What the API does (functionality)

1. Accepts a **custom list of locations** (defaults provided, but the caller can change them —
   this is how we satisfy the task's "let the user adjust the locations list" requirement at the
   API level; the UI editing comes in Phase 3).
2. For each location, fetches the conveyancing results page and **parses** firm data with
   **hand-written code (no third-party HTML libraries)**.
3. Handles each location independently and **resiliently**: one location failing, returning
   nothing, or not existing on the site must not break the whole run — it is reported as `Empty` / `Unavailable` / `Error`.
4. **Aggregates and de-duplicates** firms across locations.
5. Computes a **report** with insights (see below).
6. Returns one JSON response containing the run metadata, per-location outcomes, the de-duplicated
   firm list, and the report.

### Endpoints

| Method | Route | Purpose |
|--------|-------|---------|
| `POST` | `/api/searches` | Run a scrape for a list of locations; return results + report. |
| `GET`  | `/api/locations` | Return the known/supported locations + the 8 task defaults (for the future UI). |
| `GET`  | `/health` | Liveness check (used by Docker / smoke tests). |

`POST /api/searches` request body:
```json
{ "locations": ["London", "Birmingham", "Leeds"], "areaOfLaw": "Conveyancing" }
```
`areaOfLaw` is optional and defaults to `Conveyancing` (the only area Phase 1 supports).

## The scraping target (confirmed by inspecting the live site)

- The task names `conveyancing.html` as the submission URL, but **that page is a static guide**,
  not a results list. The real results pages follow the pattern:
  `https://www.solicitors.com/conveyancing+{location-slug}.html`
  (e.g. `conveyancing+london.html`, `conveyancing+leeds.html`).
- The listings are **server-rendered** — the data is present in the raw HTML response, so a plain
  `HttpClient` GET is enough. **No headless browser is required** despite the page's
  "JavaScript must be enabled" banner.
- Per firm, the page exposes: **name**, a **review count** in parentheses like `(330)`
  (this is a review *count*, not a star rating), **phone** (a `tel:` link), **postal address**
  (which embeds town + UK postcode), a short **description**, a **website** link, a **logo** image,
  and a **profile** link.
- There is **no raw email address** on the listing — the "Email" link points to an internal
  enquiry form (`enquiry-form.asp?...`). So "contact details" = phone + website + enquiry link.
  This will be stated honestly in the README rather than implying we scraped emails.
- The same firm can appear **multiple times** (multiple branches) and national chains appear in
  **many cities** — both are handled by de-duplication and surfaced as an insight.

## Report / insights (the "turn data into insight" requirement)

The report is computed server-side and returned as structured JSON:

- **Run summary**: timestamp, area of law, locations requested vs. succeeded vs. empty/unavailable,
  total firms found, total unique firms.
- **Per-location summary**: status, firm count, how many have reviews, average review count,
  the top firm.
- **National insights**:
  - **Multi-location firms** — firms present in 2+ cities (the dominant national players).
  - **Top firms by review count** — a prominence ranking.
  - **Coverage gaps** — locations with few/no firms (sales whitespace for the CEO).
  - **Contactability** — share of firms with a usable phone and/or website (data quality signal).

## High-level architecture

A layered (ports-and-adapters) solution. Dependencies point **inward only**:

```
        ┌─────────────────────────────────────────────┐
        │ InfoTrack.Api  (composition root, endpoints) │
        └───────────────┬───────────────┬─────────────┘
                        │               │
                        ▼               ▼
        ┌───────────────────────┐   ┌──────────────────────────┐
        │ InfoTrack.Application │   │ InfoTrack.Infrastructure  │
        │ services + ports      │◄──│ adapters (fetch/parse/    │
        │ (interfaces)          │   │ resolve) implement ports  │
        └───────────┬───────────┘   └────────────┬─────────────┘
                    │                             │
                    ▼                             ▼
              ┌──────────────────────────────────────┐
              │ InfoTrack.Domain  (pure models, enums)│
              └──────────────────────────────────────┘

  InfoTrack.Tests ── references the projects under test (xUnit)
```

- **Domain** — plain data (entities/value objects/enums). No NuGet dependencies, no logic that
  touches the outside world.
- **Application** — the use-case services (search orchestration, report building) and the **port
  interfaces** they depend on (`IListingFetcher`, `IListingParser`, `ILocationResolver`,
  `IReportBuilder`, `ISolicitorSearchService`). Depends only on Domain.
- **Infrastructure** — the **adapters** that implement the ports: HTTP fetcher (`HttpClient`),
  the hand-written HTML parser, the location→URL resolver. Depends on Application + Domain.
- **Api** — ASP.NET Core (Minimal APIs), wires everything together with DI, exposes endpoints,
  OpenAPI, error handling, config. Depends on Application + Infrastructure.
- **Tests** — xUnit; the parser and report builder are tested against **saved HTML fixtures**
  so tests run offline and deterministically.

## Definition of Done (summary)

- `docker compose up` (or `dotnet run`) starts the API with **no extra configuration**.
- `POST /api/searches` with the 8 default locations returns results + report JSON.
- unknown / empty locations are handled gracefully and reported per location.
- **No third-party HTML/scraping library** is referenced anywhere.
- Unit tests pass, including parser tests against fixtures.
- README explains how to run it, lists endpoints, shows a sample request/response, and documents
  the known caveats (enquiry-link vs. email, Bradford, review count vs. star rating).

## Non-goals for Phase 1

- No persistence/database, no EF, no history or new-firm alerting (Phase 2).
- No SPA / frontend framework (Phase 3).
- Only the `Conveyancing` area of law.
