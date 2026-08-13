# Authoring guide: scenarios, patterns, and constraints

All content lives in one versioned file,
[`src/Vector.App/wwwroot/data/vector-knowledge.v1.json`](../src/Vector.App/wwwroot/data/vector-knowledge.v1.json),
validated by
[`vector-knowledge.schema.json`](../src/Vector.App/wwwroot/data/vector-knowledge.schema.json) and
mapped into the domain by `Vector.Data.Mapping.KnowledgeMapper`. This guide keeps edits **original,
coarse, and deterministic**.

## Non-negotiables

- **Originality.** All scenario and pattern language must be original. Never copy or closely
  paraphrase certification practice questions or any employer-confidential architecture. Scenarios are
  educational examples and must not claim real-company results — include an explicit "this is an
  educational example, not a real deployment" line in every scenario framing.
- **Coarseness is honesty.** Capabilities and demand values are integers in **0–4**; weight tiers are
  **0–3**. Never introduce decimals, percentages, or "scores out of 100" into the source data. If two
  cells feel like they need a value between them, they are the same coarse value — express the nuance
  in the `reason`/rationale text instead.
- **Stable ids are permanent.** Dimension names, `PatternId` names, `scn.*`, `risk.*`, mitigation ids,
  and level names are referenced by share links, digests, and cross-references. Do not rename or reuse
  an id once shipped; retire by replacement, not mutation.

## Adding a scenario (safe, additive)

1. Add a `scenarios[]` entry with a new `scn.<slug>` id, an original 2–4 sentence framing (with the
   educational-example disclaimer), 2–3 assumptions, and a `profile` covering **all eight**
   constraints with `{ level, weightTier, hard }`.
2. Reference existing risks/mitigations implicitly (risks live on patterns and activate from the
   profile — nothing to wire per scenario).
3. Bump `contentRevision`. A new scenario cannot reorder existing scenarios' results, so this is a
   **minor** rules change at most.
4. Add a calibration test in `tests/Vector.Data.Tests/CalibrationTests.cs` asserting the intended
   qualitative read (which pattern leads, whether it is a near-tie), and run `dotnet test` — the real
   engine is the source of truth. If the read is wrong, fix the numbers, not the test.

## Adding or changing a pattern (structural — rare)

The comparison is built around exactly **four** patterns. Prefer a `variantNotes[]` entry on an
existing pattern over a new pattern. A genuine fifth pattern requires: 8 new capability values + 8
rationale strings, a review of every advisory and hard-conflict interaction, at least two risks, and
UI/space review. It is a **major** rules-version change and likely a schema review.

## Changing the decision math

Editing a capability value, a demand curve, a default weight, a level scale, or the near-tie margin
can reorder shipped results. Any such change is a **major `rulesVersion`** bump. Before committing,
run the calibration tests for all three scenarios and record the intended before/after ranking
changes. The **rules content hash** (`DigestCalculator.ComputeRulesContentHash`) changes whenever the
capability matrix, demand curves, or near-tie margin change — a drift guard even if you forget to bump
the version.

## Determinism checklist (before committing content)

- [ ] Every scenario `profile` covers all 8 constraints exactly once.
- [ ] Every constraint's `demandCurve` length equals its `levels` length; every demand value is 0–4.
- [ ] Every pattern has exactly 8 capabilities (0–4) and 8 rationale strings.
- [ ] Every risk/advisory `mitigationId` resolves against the shared `mitigations` pool.
- [ ] No decimals appear anywhere in the data.
- [ ] Enum-valued fields use the exact C# member names (`DataSensitivity`, `DirectStructuredCall`,
      `Capacity`, `GreaterOrEqual`, …); the mapper throws `DataMappingException` on any unknown value.
- [ ] `dotnet test` is green (mapper validation + calibration).

## Version fields

| Field             | Bump when…                                                        |
|-------------------|-------------------------------------------------------------------|
| `schemaVersion`   | The JSON shape changes (a field is added, renamed, or retyped).   |
| `rulesVersion`    | The decision math changes (major if it can reorder results).      |
| `contentRevision` | Only prose changes (help, reasons, framings) — never the math.    |
