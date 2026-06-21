# Phase 2 (FULL) — Robust, Low-Noise Monitoring (Description)

## Goal

Turn the honest-but-conservative change detection from Phase 2 (MVP) into a **confident monitor**.
The MVP tells the truth, but it is deliberately cautious: it flags a firm as "absent" the very first
run it goes missing (even if that was just a site blip), and it says "not comparable" whenever the
two runs being compared happen to cover different locations. FULL spends effort on the two things
that make a monitor pleasant to rely on — **fewer false alarms** and **instant answers to
current-state questions** — plus a prominence trend the stored data already makes possible.

This phase adds **no new scraping** and changes **no Phase 1 logic**. It builds entirely on the
history the MVP is already collecting.

## What the user gets (from the CEO's perspective)

- **Disappearances you can trust — no crying wolf.** Instead of being told a firm "vanished" the
  moment one scrape misses it, the tool waits for the absence to repeat across consecutive
  successful checks before calling it gone. A firm that flickers out for a single run and comes back
  is never reported as lost. Each disappearance carries a confidence: *provisional* (gone once) vs
  *confirmed* (gone repeatedly).
- **A "what's changed since last time" view that actually compares.** The MVP's default diff can say
  "not comparable" a lot when consecutive runs cover different cities. FULL picks the right baseline
  **per city** — the most recent earlier run that successfully scraped *that* city — so the CEO sees
  real new/lost firms for every city, not gaps caused by how the runs happened to be batched.
- **Instant current-state questions.** Without running a diff: "show me every firm currently active
  across all eight cities," or "everything that has appeared in the last 30 days." These become a
  single fast lookup rather than a comparison the user has to set up.
- **Prominence trends.** Because each sighting captured the review count at the time, the tool can
  show whether a firm is **rising or fading** in prominence over the runs — another way to spot
  firms worth a call.

## Why this scope

- The MVP is correct but conservative by design; its own non-goals list is exactly this phase's
  backlog. Everything here *refines* a working feature rather than enabling a new one, which is why
  it is safe to ship after the MVP and easy to describe as "the polish that makes it a real monitor."
- All of it is computable from the MVP's normalised history (runs, location outcomes, firms,
  sightings). No schema rework of the core tables is required — FULL mostly **adds derived state and
  smarter queries** on top.

## What's new (functionality)

1. **Confirmed disappearance (debounce / hysteresis).**
   - A firm absent from a successfully-scraped location is **provisionally absent** on the first
     miss and **confirmed gone** only after it has been absent across **N consecutive successful
     observations of that location** (default `N = 2`, configurable). "Consecutive" is measured
     along that *location's* observation timeline — runs where the location failed or wasn't
     requested are skipped, not counted as absences.
   - Any reappearance **resets** the counter and clears the flag. Disappearance remains soft
     (`LastSeenAt` driven); the confidence label is derived, never a destructive write.
   - The diff response gains a `Confidence` field on each absent/dropped firm
     (`Provisional` | `Confirmed`).
2. **Per-location baseline selection for the default diff.**
   - The "compare against last time" default no longer fixes one global earlier run. For each
     location it selects the most recent earlier run that observed *that location* successfully,
     dramatically reducing `NotRequested` / `ScrapeFailed` verdicts in the default view.
   - The explicit two-run diff (`diff?against={id}`) is unchanged — when the user names two runs,
     those two are honoured and non-comparable locations are still labelled as in the MVP.
3. **Current-firms projection + queries.**
   - A derived `CurrentFirms` view (rebuilt/rolled forward from sightings, **never** a separate
     source of truth) exposing per firm: identity, latest attributes, `FirstSeenAt`, `LastSeenAt`,
     the locations it is currently active in, and derived status (`Active` / `ProvisionallyAbsent` /
     `ConfirmedGone`).
   - `GET /api/firms?status=active`, `GET /api/firms?addedSince={date}` (and similar) for instant
     current-state answers without setting up a diff.
4. **Review-count trend.**
   - `GET /api/firms/{id}/history` (or a trend field on the firm view) showing review count over
     successive sightings — a simple rising/steady/falling prominence signal.

## What this builds on (no core schema rework)

- **Runs, LocationOutcomes, Firms, Sightings** from the MVP are sufficient inputs. The
  `LocationOutcome.Status` history is what powers the "consecutive successful observations" logic.
- **`CurrentFirms`** is a **projection**: a convenience/materialised view derived from sightings. It
  can be recomputed from history at any time, so it is disposable and never authoritative. Losing or
  rebuilding it changes nothing about the truth.

## Definition of Done

- A firm that goes missing for a single successful run is reported as **provisionally absent**, not
  gone; after `N` consecutive successful misses it is reported as **confirmed gone**; a reappearance
  resets it. Covered by tests, including the skip-over-failed-runs case and the
  flicker-then-return case.
- The **default** diff selects an earlier baseline **per location** and produces real comparisons
  for every location with sufficient history; the explicit two-run diff retains MVP behaviour.
- `GET /api/firms?status=active` and `?addedSince=` return correct results sourced from the
  `CurrentFirms` projection, and the projection can be rebuilt from history to the identical result.
- Review-count history is queryable per firm.
- All Phase 1 and Phase 2 (MVP) tests still pass unchanged; the new debounce, baseline-selection,
  projection, and trend logic are unit-tested as pure functions where possible.
- README documents the confirmation threshold `N` (and how to configure it), the per-location
  baseline behaviour, the current-firms queries, and that `CurrentFirms` is a rebuildable
  projection.

## Non-goals for Phase 2 (FULL)

- Still **no frontend** — visualising new/confirmed-gone/active firms is Phase 3.
- **No scheduled/automatic scraping.** Runs are still triggered by `POST /api/searches`; the
  confirmation logic works over whatever runs exist, manual or otherwise. A scheduler is a later
  enhancement.
- **No areas of law beyond Conveyancing.**
- No external notifications (email/Slack alerts) — "alerting" here means the data is computed and
  exposed via the API; pushing it elsewhere is out of scope.
