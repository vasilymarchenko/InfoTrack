# Architecture Decision Records — Phase 2

Continues `ADRs.md` (ADR-001 … ADR-015). These records cover the persistence and
change-detection decisions introduced in Phase 2. Only decisions that shaped the architecture and
had real alternatives are recorded here; smaller implementation choices live in
`PHASE-2-MVP-implementation.md`.

---

## ADR-016: Persist observations as immutable per-run records; derive interpretations on read

**Status:** Accepted

**Context.** Phase 1 is stateless — results are returned and discarded. Phase 2 needs memory so the
tool can detect change. There are two ways to hold that memory. One is to keep a single mutable
"current list of firms" and update it on every scrape (insert new firms, flag missing ones). The
other is to record each run immutably — exactly what the site showed at that moment — and compute
"new" and "disappeared" by comparing runs. We also had to decide whether to store the computed
report or regenerate it.

**Decision.** The scraped observations are the single source of truth, and they are immutable: each
run is stored as it was seen and never edited afterwards. Everything interpretive — the report, and
the new/disappeared diff — is computed from those observations when read, not stored. We do not keep
a mutable "current state" table as the source of truth, and we do not persist the report.

**Consequences.**
- Any past run can be re-read exactly as found, any two runs can be compared, and the
  interpretation logic can improve later (e.g. a smarter diff) without migrating data.
- A stored report can never drift from the data it summarises, because there is no stored report.
- More work happens on read instead of write. At this project's scale (a handful of cities, a few
  hundred firms, manual runs) this is negligible.
- A "current state" question (e.g. "all firms active right now") is not a single-row lookup. If that
  becomes valuable it is built as a derived projection (Phase 2 FULL) — rebuilt from these
  observations, never treated as the truth itself.

**Alternatives considered.** A mutable current-state table as the source of truth was rejected: it
bakes in one interpretation and throws away the ability to recompute history. Persisting the report
was rejected as a stored interpretation that can fall out of sync with its source.

---

## ADR-017: Store each firm once, with cheap per-run "sightings"

**Status:** Accepted

**Context.** Given that we keep an immutable record of every run (ADR-016), the naive shape re-stores
every firm's full details on every run. Across repeated runs this copies the same unchanging data —
name, address, description, logo — many times over, for no benefit.

**Decision.** Store each firm's attributes **once**, keyed by firm identity. Record each run's
presence as a small **sighting** row linking a run and location to a firm, carrying only the review
count — the one field that legitimately changes over time. The attributes on a firm's row are the
**latest observed** values, refreshed on each save.

**Consequences.**
- Storage is proportional to the number of distinct firms plus small sighting rows, not to
  runs × firms. The duplication concern that motivated this decision disappears.
- Review-count history is preserved per sighting, which makes prominence trends possible later
  (Phase 2 FULL) at no extra storage cost now.
- The diff becomes a set comparison over sightings.
- Trade-off (accepted): re-reading an old run shows that run's review counts but the firm's
  *current* address, phone, etc., because per-run attribute history is not kept. The firm identity
  used by the diff is stable, so change detection is unaffected; avoiding duplication is worth more
  to this product than perfect point-in-time attribute fidelity.

**Alternatives considered.** A "fat snapshot" (re-storing every firm's full attributes on every run)
was rejected — it is duplication with no product value, and was the specific shape this decision
exists to avoid.

---

## ADR-018: Compute change per location, only where both runs observed the location successfully

**Status:** Accepted

**Context.** The product's value is *trustworthy* change detection. Two situations threaten it.
First, a location can fail or return nothing in a given run — a network error, a site problem, or a
genuinely empty page. Second, different runs may cover different sets of locations. A naive "firms in
run A minus firms in run B" would invent change in both cases: it would report every firm in a failed
location as "disappeared", and every firm in a newly added location as "new".

**Decision.** The diff is computed **per location**, and a new/disappeared claim is made **only** for
locations that returned `Success` in **both** runs being compared. Every other location is reported
with an explicit verdict — `NotRequested` or `ScrapeFailed` — and carries no firm-level change. Firm
identity for the comparison uses one canonical key (normalised name + postcode, falling back to
phone), the same key used by Phase 1 de-duplication and by persistence.

**Consequences.**
- The tool never fabricates arrivals or disappearances from a broken or skipped scrape. A failed
  Bradford run cannot trigger a false mass-disappearance.
- Output is honest: the user is told *why* a location cannot be compared, rather than being shown a
  fake result.
- When consecutive runs cover different locations, the default comparison shows many "not
  comparable" locations. Reducing this by choosing a baseline *per location* is deliberately deferred
  to Phase 2 FULL.
- Correctness depends on the firm-identity key being computed identically at de-duplication,
  persistence, and diff time — which is why that key is defined in exactly one place.

**Alternatives considered.** A global set difference across all firms was rejected because it lies
whenever a location fails or whenever runs cover different locations.

---

## ADR-019: Model disappearance as absence computed from sightings — never a destructive flag

**Status:** Accepted

**Context.** We must report firms that "disappeared". But a firm missing from a single run is not
necessarily gone for good — it may be a transient site or parsing glitch, and it may return on the
next run. A stored boolean "deleted" flag on the firm was considered.

**Decision.** We never store a "deleted" flag and never delete a firm. Disappearance is **derived**:
a firm is "absent" in a comparison when it has no sighting in the later run's successful observation
of that location. Each firm keeps `FirstSeenAt`/`LastSeenAt` for information, but its presence is not
stored as state. A firm that returns simply gains a new sighting — no special handling, and no false
"death" was ever recorded.

**Consequences.**
- Reappearance is handled correctly and for free; absence is reversible, which matches how listing
  sites actually behave.
- No destructive writes — the observation record stays intact and fully recomputable (consistent
  with ADR-016).
- In the MVP, a single-run absence is reported as-is and clearly labelled, without judging whether
  it is permanent. Confirming a disappearance across several consecutive successful runs (debounce)
  is deliberately deferred to Phase 2 FULL.

**Alternatives considered.** A boolean "deleted" marker was rejected because it treats a reversible
observation as a permanent fact, and would actively mislead when a firm returns.

---

## ADR-020: Split Phase 2 into MVP (correct and honest) and FULL (low-noise and convenient)

**Status:** Accepted

**Context.** The full change-detection vision includes robustness and convenience features:
confirming disappearances across several runs, choosing comparison baselines per location, a fast
"current firms" view, and review-count trends. Building all of it before shipping would delay the
core value and would mix "must be correct" concerns with "nice to have" ones.

**Decision.** Split Phase 2 on a single seam. **MVP** delivers everything required for change
detection to be **correct and honest** — persistence, history, and the success-gated per-location
diff (ADRs 016–019). **FULL** adds only what **reduces false alarms or answers current-state
questions faster** — disappearance confirmation/debounce, per-location baseline selection, a
rebuildable current-firms projection, and review-count trends. The correctness rules (ADR-018,
ADR-019) belong in the MVP, because a diff that can lie is worse than no diff; the
robustness and convenience refinements belong in FULL.

**Consequences.**
- The MVP is independently shippable and defensible. Deliberate absences — for example, no
  disappearance debounce — are documented as design choices, not gaps.
- FULL requires no rework of the MVP's core tables; it is computed from the same observations.
- Until FULL ships, the default comparison is conservative: more locations marked "not comparable",
  and single-run absences shown without confirmation.
