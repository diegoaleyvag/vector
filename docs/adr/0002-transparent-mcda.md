# ADR 0002 — A transparent, deterministic MCDA engine

- **Status:** Accepted
- **Date:** 2026-08-13

## Context

Vector must *support* a decision, not declare an answer. The scoring therefore has to be transparent
(every contribution inspectable), deterministic (same inputs → same outcome and digest), honest about
precision (no fake decimals), and — critically — **responsive to the constraints the user sets**, so
that changing a requirement visibly changes the reasoning. Hard requirements must behave differently
from weighted preferences.

## Decision

A versioned, **integer-only** multi-criteria model (full detail in
[decision-method.md](../decision-method.md)):

- **Weighted unmet-demand penalty.** For each (pattern, constraint), `shortfall = max(0, demand −
  capability)`; the pattern's score is `SCALE − 25·Σ weightBp·shortfall`. Contributions sum exactly to
  the score (a tested accounting identity). Weights are integer basis points summing to 10000 via
  Hamilton/largest-remainder apportionment from coarse 0–3 tiers.
- **Hard gating is a separate pass.** A hard constraint with `capability < demand` vetoes the pattern
  regardless of weight; vetoed patterns are shown but always rank below eligible ones. A hard conflict
  can never be hidden by a high score.
- **Near-tie and one-at-a-time sensitivity** are reported so close results read as ties and pivotal
  constraints are surfaced.
- **A culture-invariant SHA-256 configuration digest** over integers makes any decision reproducible
  and verifiable across platforms.

### Why unmet-demand, and not `capability − demand`

We first specified a fixed-range normalization of the raw fit `capability − demand`. During
implementation we proved it was **shift-invariant in the level**: because `demand` is identical across
all four patterns, a level change shifts every pattern's score by the same amount, so it **cancels in
every pairwise comparison** — a constraint's *level* could never re-order the soft ranking, only a
hard veto could. That would have hollowed out the tool's central "test sensitivity by changing a
constraint" feature. The `max(0, …)` clip of the unmet-demand model breaks the symmetry (raising a
demand penalises patterns that fall short more than those that already meet it), restoring genuine
level sensitivity while preserving determinism, integer math, and the hard/soft separation. This
correction is captured by a regression test that asserts a single level change re-orders eligible
patterns with no veto involved.

## Consequences

- **Positive:** the ranking responds to both levels and weights; every `+`/`−` contribution has a
  numeric value and an authored reason; results are bit-identical and reproducible from a digest; hard
  conflicts are structurally impossible to bury.
- **Trade-offs:** over-meeting a demand earns no credit (correct for a *fit* question, but it means
  low-demand scenarios can legitimately tie several patterns); coarse 0–4 values mean small score gaps
  are not meaningful — hence the near-tie flag and the "close scores are ties" guidance.
- **No LLM in the loop:** the outcome is a deterministic computation, never a generated conclusion.
