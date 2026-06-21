# Architecture Decision Records — Phase 2 (continued: listing rotation)

Continues `ADRs-phase-2.md` (ADR-016 … ADR-020). These records capture decisions that arose from an
**empirical observation made during Phase 2 (FULL)**: consecutive runs of the *same* location
frequently return a *different subset* of firms even when nothing has truly changed — the target site
appears to rotate, re-rank, or partially sample its listing. As before, only decisions that shaped
the architecture and had real alternatives are recorded.

---

## ADR-021: Confirm change across a window of consecutive successful runs

**Status:** Accepted

**Context.** The MVP asserts a change from a single comparison — a firm present in one run and absent
in the next is "disappeared". In practice the target site returns a rotating/partial sample of each
location's firms, so a truly-listed firm is regularly missed for reasons that have nothing to do with
real change. Under single-run comparison this produces frequent false disappearances (and false
arrivals) that are pure sampling noise.

**Decision.** A change is only **confirmed** when it holds across a window of **K consecutive
successful runs** of that location. The unit of distance is the **successful** run
(`Status == Success`); non-success runs carry no information and are skipped, never counted as
absences. A firm reappearing at any point in the window **resets** any pending "gone" confirmation.
`K` is a configurable window (default **3**), tuned to the site's observed per-scrape coverage.

**Consequences.**
- Sustained, real change is confirmed; transient rotation flicker is not.
- Reappearance is handled naturally by the reset, so a flickering firm can never confirm as gone.
- Confirmation lags reality by up to `K` successful runs. This is accepted: for a sales-lead signal a
  false confirmation is worse than a slightly delayed one.
- `K` must be tuned to the rotation — too small risks false confirmations under heavy rotation, too
  large slows confirmation. A single **global** `K` is used for now; a **per-location** `K` was
  considered and deferred, as there is not yet evidence that locations rotate differently enough to
  justify the added complexity.
- Builds directly on ADR-019 (disappearance is derived absence): this decision defines *how much*
  absence is enough to act on.

**Alternatives considered.** Single-run change detection — retained for the explicit two-run diff,
but rejected as the basis for a trustworthy "what changed" signal because rotation makes it report
sampling noise as change. A **time-based** window (e.g. absent for N days) — rejected because runs
are manual and irregular; the meaningful unit is successful runs observed, not elapsed time.

---

## ADR-022: Confirm appearances as well as disappearances (symmetric confirmation)

**Status:** Accepted

**Context.** The earlier plan applied confirmation only to disappearances and reported new firms
immediately. The rotation observation (ADR-021) undermines that asymmetry: if each scrape surfaces
only a subset, a firm appearing for the *first time* may be genuinely new **or** merely sampled for
the first time. Treating every first appearance as a confirmed new lead would manufacture false "new
firm" signals in exactly the way naive comparison manufactured false "gone" signals.

**Decision.** Confirmation is **symmetric**. A firm is a **Confirmed** new arrival only when it
appears after a clean prior window of `K` successful runs in which it was absent — i.e. we had enough
samples to trust it was genuinely not there before. Otherwise it is **Provisional**. Disappearances
follow the same rule in the opposite direction (ADR-021). One confirmation mechanism serves both
directions.

**Consequences.**
- "New lead" carries the same trustworthiness as "gone"; both are earned over the same distance.
- A single confirmer handles both directions, keeping the model simple to reason about and test.
- A genuinely new firm is only **Confirmed** once `K` prior clean runs exist; before that it is shown
  as **Provisional** (ADR-023) rather than withheld, so lead visibility is not blocked.
- Early in a location's history, most changes are Provisional until enough runs accumulate. Accepted.

**Alternatives considered.** Confirming only disappearances while treating appearances as immediate
(the earlier plan) — rejected once rotation was observed, because appearances are equally subject to
sampling noise.

---

## ADR-023: Surface Provisional vs Confirmed; never confirm without sufficient history

**Status:** Accepted

**Context.** Confirmation over a window (ADR-021, ADR-022) creates an unavoidable in-between state: a
change has been observed but not yet established across `K` runs. We had to decide what to do with
that interim signal, and what to report when the available history is shorter than the window.

**Decision.** Every reported change carries an explicit confidence — **Provisional** or
**Confirmed** — and **both are shown**. A change is not hidden until confirmed; it appears
immediately as Provisional and is promoted to Confirmed once it holds across the window. When history
is shorter than the window, a change is **always Provisional, never Confirmed** — the system never
claims certainty it has not earned.

**Consequences.**
- Early signals are visible immediately — no genuinely new lead is delayed by `K` runs — while their
  uncertainty is explicit, so users can act on Provisional leads at their own discretion and rely on
  Confirmed ones.
- The "insufficient history ⇒ Provisional" rule prevents over-claiming on young datasets.
- Consumers (and any future UI) must handle two confidence levels rather than a single
  changed/unchanged flag.
- Provisional will be the common state early in a location's history; this is expected and is
  communicated rather than concealed.

**Alternatives considered.** Suppressing changes until Confirmed (show nothing until the window is
satisfied) — rejected because it delays every genuinely new lead by up to `K` runs and hides useful
early signal. A single binary "changed" flag with no confidence — rejected because it forces a
premature true/false call that rotation makes unreliable.
