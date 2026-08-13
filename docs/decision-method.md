# The Vector decision method

Vector ranks four architecture patterns against a constraint profile using a small, deterministic,
integer-only multi-criteria model. Every number and every rule below is inspectable in the UI; this
document explains what they mean and why the model is shaped the way it is.

The four patterns are: **Direct structured model call**, **Deterministic workflow with model steps**,
**Retrieval-augmented generation (RAG)**, and **Tool-using agent**.

## 1. Inputs

### Constraints, levels, and demand

There are **eight constraints** (the canonical order is load-bearing — it drives scoring, the trace,
and the digest):

1. Data sensitivity · 2. Latency target · 3. Cost pressure · 4. Determinism/reproducibility ·
5. Knowledge freshness · 6. Tool/action need · 7. Human review · 8. Operational maturity.

Each constraint has a **typed ordinal scale** (small named levels such as *Public … Restricted* or
*Stable … Current*) and a **demand curve** that maps each level index to a **demand value in 0–4**.
Seven constraints are *demand* constraints — a higher level means a stronger requirement, so the
curve increases (e.g. `[0, 2, 3, 4]`). **Operational maturity is a *capacity* constraint** — a higher
level means the team can support *more* operational complexity, so its curve is **inverted**
(`[4, 3, 1, 0]`): a nascent team *demands* high operational simplicity, an advanced team demands
little. Expressing polarity in the data keeps the scoring engine uniform, with no special cases.

### Capabilities

Each pattern has a **capability value in 0–4** on every constraint (e.g. a direct call has high
cost-efficiency and containment but zero freshness and tool capability). Capabilities and demand
values are deliberately coarse integers — the honesty of the tool depends on *not* inventing decimal
precision. Nuance lives in the authored **reason text** attached to every capability, not in the
numbers.

### Weights

For each constraint you choose an importance **tier** — *Ignore (0), Low (1), Medium (2), High (3)*.
Tiers are converted to **basis-point weights that always sum to exactly 10000** using the
**Hamilton / largest-remainder** apportionment method (deterministic, integer, ties broken by
ascending dimension). If every tier is 0, weight is distributed equally. This keeps the weighted sum
exact and platform-independent.

### Hard requirements

Any constraint can be marked **hard**. Hard requirements are handled by a *separate* gate (Section 3),
independent of weight — so a hard conflict can never be hidden behind a high weighted score.

## 2. Scoring: weighted unmet-demand

For each pattern `p` and constraint `c`, in canonical order:

```
demand    = demandCurve(c, level)          # 0..4, from the constraint's curve
capability = capability(p, c)              # 0..4
shortfall  = max(0, demand - capability)   # 0..4  — the nonlinear clip
```

The score is built from **shortfall**, not from a raw `capability - demand` difference. This choice is
deliberate and important:

> If we scored `capability - demand` and summed it, a pattern's score would shift by the *same*
> amount for every pattern when a constraint's level changed (because `demand` is identical across
> patterns). The level would then cancel out of every pairwise comparison, and **changing a
> constraint's level could never re-order the ranking** — only a hard veto could. That would gut the
> entire point of a "compare under explicit constraints" tool. The `max(0, …)` clip breaks that
> symmetry: raising a demand penalises the patterns that fall *short* of it more than the ones that
> already meet it, so the level genuinely changes the ranking.

Per-constraint contribution and the pattern score (all integer, on a fixed-point `SCALE = 1_000_000`):

```
weightedContribution(c) = weightBp(c) * (100 - 25 * shortfall)     # in [0, weightBp*100]
scoreScaled(p)          = Σ_c weightedContribution(c)              # in [0, SCALE]
                        = SCALE - 25 * Σ_c weightBp(c) * shortfall(c)
```

Because `SCALE / 10000 = 100` and the weights sum to `10000`, a pattern that **meets every demand
scores exactly `SCALE`** (a perfect fit), and the per-constraint contributions **sum exactly to the
score** (an accounting identity we test). A pattern that falls short only pays for the constraints you
weighted. Over-meeting a demand earns no bonus — the question is *"does it fit the constraint?"*, not
*"how far past it can it go?"*.

The displayed `Score` is `scoreScaled / SCALE`; it is for display only. All ranking math is integer.

## 3. Hard-constraint gating

Independently of the score, for each constraint marked **hard**:

```
if capability(p, c) < demand(c, level):   →  p has a HARD CONFLICT on c  (a veto)
```

A pattern with any hard conflict is **Vetoed**; otherwise it is **Eligible**. Weight is irrelevant
here — **a hard constraint with weight *Ignore* still vetoes.** Vetoed patterns are **kept and shown**
(with their violated constraints listed) so the reasoning stays transparent, but they always rank
below every eligible pattern.

## 4. Ranking

Patterns are ordered by a composite key:

1. **Hard status** — Eligible before Vetoed (a veto can never be out-scored);
2. **Score** — higher first;
3. **Pattern id** — ascending, a deterministic tie-break.

## 5. Near-tie

Among *eligible* patterns, the **top margin** is `score(rank 1) − score(rank 2)`. If it is smaller
than `NearTieMarginBasisPoints` (300 bp = 3% of `SCALE` = 30 000 scaled units), the result is flagged
as a **near-tie**: the top two are effectively co-leading and should not be read as strictly ranked.

## 6. Sensitivity (one-at-a-time)

For each constraint, holding all others fixed, Vector re-evaluates at every alternative level and
reports:

- whether a single **±1 step** changes the eligible leader (a **pivotal** change), and
- the **minimum flip distance** — the smallest single-constraint level change that flips the leader
  (or "robust" when none does within the scale).

This is exactly the analysis the unmet-demand model makes meaningful (Section 2).

## 7. Determinism and the configuration digest

The engine is a pure function of `(profile, ruleset)`. To make a decision reproducible and
verifiable, `DigestCalculator` computes a **SHA-256 configuration digest** over integers only,
big-endian, with length-prefixed UTF-8 for version strings and **no culture-dependent formatting**:

```
"Vector.MCDA" · EngineVersion · RulesVersion · RulesContentHash · SCALE · RawMin(-4) · RawMax(+4)
  · for each dimension in canonical order: (dimension code, level index, weight tier, isHard)
→ SHA-256 → "Sha256:" + lowercase hex
```

Because the digest hashes the **weight tier** (not the apportioned basis points) and only integers, it
is identical across platforms and cultures (verified under `tr-TR` and `de-DE`). Scenario metadata
(id, title, description) is **excluded** — it does not change the decision. A separate
**rules content hash** covers the capability matrix, demand curves, and near-tie margin, so rule drift
is detectable even if the rules version is not bumped.

## 8. Versioning

- **Engine version** (`1.0.0`) — the algorithm and digest layout. Bump on any change to the math.
- **Rules version** (`1.0.0`) + **rules content hash** — the capability matrix, demand curves, scales,
  and thresholds. Stamped into every result and into share links.
- **Content revision** — prose-only edits (help, reasons, framings) that never change the math.

Every `DecisionOutcome` carries a `VersionStamp`, so an archived record is exactly reproducible. See
[authoring-guide.md](authoring-guide.md) for the rules that keep this stable.
