# Limitations and non-claims

Vector is a **decision-support teaching tool**. It compares four AI-system architecture patterns
under constraints that *you* set, and it shows its reasoning. Please read what it is — and is not —
before relying on it.

## What Vector does

- It ranks how well each pattern **fits the constraints you entered**, using a transparent,
  versioned multi-criteria model whose weights, rules, and per-contribution reasons are all visible.
- It separates **hard requirements** (which can make a pattern non-viable) from **weighted
  preferences** (which shift the ranking but never hide a hard conflict).
- It exports an ADR-style Markdown record so a decision is traceable and reproducible.

## What Vector does not do

- **It does not tell you the "correct", "best", or "recommended" architecture.** It offers decision
  *support*; the judgement is yours.
- **It is not certification, compliance, audit, or professional advice**, and it implies no
  endorsement or best practice. A high score is not an approval.
- **It makes no claim about any real organization, product, or deployment.** The three scenarios are
  original educational examples. They describe no real system and report no real results.
- **It is not an LLM recommender.** No language model produces the ranking; the outcome is a
  deterministic computation over the inputs and the versioned rule data.

## How to read the numbers

- Scores are **coarse ordinal judgements, not measurements.** The underlying capability and demand
  values are small integers (0–4) by design, to avoid fake precision. Do not over-read small gaps.
- **Close scores are effectively ties.** Vector flags a near-tie at the top of the ranking; treat
  flagged pairs as co-leading rather than ranked.
- The ranking is **only as good as the assumptions you set.** Change the levels and weights and watch
  the reasoning update — that exploration is the point, not any single ranking.
- The sensitivity panel shows where a **single change would flip the leading option**. A robust
  result is one that does not flip under small changes; a pivotal one deserves more scrutiny.

## Versioning and reproducibility

Every result carries the **engine version**, the **rules version**, and a **configuration digest**.
The same inputs under the same versions always produce the same outcome and digest. A shared link
recomputes against the current rules and will differ (and should be re-examined) if the rules have
changed since the link was made.
