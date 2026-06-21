# Phase 2 (MVP) — History & Trustworthy Change Detection (Description)

## Goal

Give the tool a **memory**. Every search run is saved to **PostgreSQL** (via **EF Core**), past
runs can be listed and re-opened, and any two runs can be **compared to show which firms appeared
and which disappeared** — the task's "detect and alert on new solicitors" feature.

The defining constraint of this MVP is not just *that* we detect change, but that the change we
report is **honest**: we never present a failed or un-checked location as if firms had arrived or
vanished there. A diff the CEO can't trust is worse than no diff at all, so the correctness rules
that prevent false alarms are part of the MVP, not deferred.

## What the user gets (from the CEO's perspective)

- **A running history instead of a disposable snapshot.** Phase 1 threw the results away after the
  response. Now every run is kept, so "last week" actually exists to compare against.
- **"Which firms are new since last time?"** — answered directly. After two or more runs, the tool
  shows, per city, the firms that weren't there before. These are the leads the sales team hasn't
  seen.
- **"Which firms have dropped off?"** — the inverse signal, shown per city: firms that were present
  before and aren't showing now.
- **No false alarms from broken scrapes.** If a city failed to load, returned nothing, or simply
  wasn't requested this time, the report says exactly that for that city — it does **not** pretend
  every firm there appeared or disappeared. The CEO sees "not comparable: scrape failed" rather
  than a fake mass-arrival or mass-extinction.
- **Re-open any past run** to see exactly what it found, with its report regenerated.

## Why this scope (why it's the MVP, not the whole of Phase 2)

- It delivers the headline bonus feature — change detection — end to end, on a real database, with
  the layering from Phase 1 untouched.
- It includes the **correctness floor** (gating change claims on successful scrapes, and labelling
  every non-comparable location) because that floor is what makes the feature trustworthy. Removing
  it would make the diff actively misleading.
- It deliberately stops short of the **noise-reduction and convenience** work (confirming
  disappearances over several runs, smarter baselines, instant current-state queries, prominence
  trends). Those are real value but they *refine* a feature that already works; they belong in
  Phase 2 (FULL).

## What changes / what's new (functionality)

1. **Persistence.** `POST /api/searches` still scrapes and returns results, but now also saves the
   run. Storage is **normalised**: a firm's attributes are stored **once**, and each run records
   cheap **sightings** ("firm F was seen in run R, in location L"). We do not re-copy every firm's
   full details on every run.
2. **History.**
   - `GET /api/searches` — list past runs (id, timestamp, area of law, locations, totals).
   - `GET /api/searches/{id}` — re-open a stored run with its (recomputed) report.
3. **Change detection / alerts.**
   - `GET /api/searches/{id}/diff?against={previousRunId}` — and a convenience default that compares
     against the most recent earlier run — returns, **per location**, the firms that are **new** and
     the firms that are **absent** since the comparison run.
   - Each location in the diff carries a **comparability verdict**: `Comparable`, `NotRequested`
     (the location wasn't in one of the two runs), or `ScrapeFailed` (the location returned
     Empty / Unavailable / Error in one of the runs). New/absent lists are only populated when the
     verdict is `Comparable`.
4. **Postgres in Docker Compose**: a `db` service is added alongside `api`.
5. **SQL schema script**: a `db/schema.sql` (generated from EF migrations) is committed, and the
   README documents where the connection string lives.

## The change-detection contract (the important part)

A change claim is only made about a location where **both** runs observed it **successfully**
(`Status == Success`). Everywhere else, the tool reports *why* it can't compare rather than
inventing a result. Within a comparable location, using the Phase 1 firm-identity rule
(normalised firm name + postcode, fallback phone):

- **New** = present in the newer run, absent in the older. Reported immediately — a firm seen for
  the first time in a city we have a real baseline for is a genuine new lead.
- **Absent** = present in the older run, absent in the newer. Reported as **"absent since the
  comparison run"** — *not* as a permanent deletion. The MVP makes the single-run statement
  honestly and leaves "is it really gone or just a blip?" to the human (the confirmation logic that
  answers that automatically is Phase 2 FULL).

Disappearance is never stored as a boolean `deleted`. Each firm carries `FirstSeenAt` and
`LastSeenAt`; "active" is derived as *seen in the latest successful run of that location*. If a firm
reappears, `LastSeenAt` simply advances — no false death was ever recorded, and recovery costs
nothing.

## Architecture changes

Phase 1's layering is preserved; persistence is a new adapter behind a new port.

- **Application** gains `ISearchRunRepository` (save a run, get by id, list runs, find the most
  recent earlier run) and a pure `RunComparer` service that produces the per-location diff with
  comparability verdicts. Application depends only on the interface.
- **Infrastructure** gains the EF Core implementation (`AppDbContext`, entity configurations,
  `EfSearchRunRepository`) on the **Npgsql** provider.
- **Domain** is unchanged. Scraping adapters (fetcher/parser/resolver) are unchanged.
- **Api** registers the DbContext + repository, applies migrations on startup, and exposes the new
  history/diff endpoints.

```
Application:  ISolicitorSearchService ─► ISearchRunRepository (new port)
              RunComparer (pure, per-location, Success-gated)
                                            ▲
Infrastructure:                             └─ EfSearchRunRepository ─► AppDbContext ─► PostgreSQL
```

## Persisted data model (conceptual)

- **SearchRun** — `Id`, `RunAtUtc`, `AreaOfLaw`, `RequestedLocations`, summary totals.
- **LocationOutcomeRecord** — belongs to a run: `Location`, `RequestedUrl`, `Status`,
  `ErrorMessage`. This is what lets the diff know whether a location was observed successfully.
- **Firm** — **one row per firm identity** (normalised name + postcode): the stable attributes
  (name, address, town, postcode, phone, website, enquiry url, profile url, description, logo url),
  plus `FirstSeenAt` and `LastSeenAt`.
- **Sighting** — one small row per (run × firm × location): the fact that a firm was seen in a run,
  plus `ReviewCount` captured at that time (the one attribute that legitimately drifts and drives
  ranking). The diff is a set comparison over sightings.

## Definition of Done

- `docker compose up` starts **both** `api` and `db`; the API migrates the schema automatically with
  no manual DB setup.
- `POST /api/searches` persists a run (normalised firms + sightings) and returns its `RunId`.
- `GET /api/searches` lists runs; `GET /api/searches/{id}` re-opens a stored run with its report.
- `GET /api/searches/{id}/diff` returns **per-location** new and absent firms, **only** for
  locations that were `Success` in both runs, and **labels every other location** with a
  comparability verdict. Covered by tests, including: a location present in only one run; a location
  that failed in one run; identical runs → empty diff; a firm that disappears then reappears.
- Disappearance is modelled via `LastSeenAt` (no boolean delete); reappearance advances it cleanly.
- `db/schema.sql` is committed and reproduces the schema; the README documents the connection
  string and the change-detection contract above.
- All Phase 1 tests still pass.

## Non-goals for Phase 2 (MVP) — explicitly deferred to Phase 2 (FULL)

- **Confirmed disappearance / debounce.** The MVP reports a single-run absence honestly but does not
  yet wait for several consecutive successful absences before calling a firm "likely gone." (FULL.)
- **Per-location baseline selection** for the default "since last time" view. The MVP's default
  compares against one global earlier run and marks non-comparable locations; choosing the most
  recent earlier run *per location* (so the view says "not comparable" far less often) is FULL.
- **Current-firms projection** for instant "who is active right now / what was added this month"
  queries. (FULL.)
- **Review-count trend** over time. (FULL.)
- Still **no frontend** (Phase 3), **no scheduler**, and **no areas of law beyond Conveyancing**.
