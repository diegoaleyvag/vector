# HANDOFF — Vector

Status: **foundation vertical slice, in progress.** Git remote:
`git@github.com:diegoaleyvag/vector.git`. Integration work is on
`feat/five-decisions-integration`
([PR #1](https://github.com/diegoaleyvag/vector/pull/1)); default branch remains `main` until an
owner merges. Repository visibility is private; no production promotion or stable public demo is
claimed here. Commits are authored solely by the repository owner.

## Environment

- macOS (arm64). **.NET 10 SDK `10.0.400`** installed via `brew install dotnet`
  (`DOTNET_ROOT=/opt/homebrew/opt/dotnet/libexec`, `dotnet` on `PATH`).
- Solution uses the new `.slnx` format (`Vector.slnx`); `global.json` pins the SDK feature band.
- No workloads required — standard (non-AOT) Blazor WebAssembly builds with the base SDK.

## Commands (from the repository root)

```bash
dotnet build Vector.slnx -c Release
```

```bash
dotnet test Vector.slnx
```

```bash
dotnet run --project src/Vector.App
```

```bash
dotnet format --verify-no-changes
```

```bash
dotnet publish src/Vector.App -c Release
```

## Test evidence

Run the full gate from the repository root:

```bash
dotnet test Vector.slnx
dotnet format --verify-no-changes
```

| Suite | Focus |
|---|---|
| `Vector.Engine.Tests` | Determinism, golden + culture-invariant digest, hard-veto dominance, accounting identity, order-invariance, soft-score level flip, near-tie, sensitivity, architecture (no Blazor/JSON in Domain/Engine). |
| `Vector.Data.Tests` | Content load/mapping validation, calibration vs. the real engine (see below), share-codec round-trip/fuzz/never-throws/decompression-bound, Markdown export golden + no-auto-fill. |
| `Vector.App.Tests` | bUnit core-flow tests (scenario load, live recompute, hard-conflict persistence, contribution inspection, chart textual alternative, sensitivity, no-auto-fill rationale, share round-trip, bad-link rejection, export interop, accessible names, non-claims copy, presentation/manifest checks). |

All suites must pass before release review. Re-run after any engine, data, content, or UI copy change.

### Browser verification (real WebAssembly runtime)

Ran `dotnet run --project src/Vector.App` and exercised the app in a browser: the studio loads;
selecting *Internal policy assistant* reproduces the calibrated ranking live (RAG 928,575 leading);
**Copy share link** produces a `v1.` fragment and announces "Link copied"; reloading that link
hydrates the exact profile; **Export as Markdown** downloads `vector-decision-record.md` (contains the
`Sha256:` digest and the literal `[[ ]]` rationale prompts); a bad link (`#v9.…`) falls back to a blank
profile with a "different version of Vector" banner. No unhandled exceptions.

### Calibration (real engine, real content)

| Scenario | Leading | Reading |
|---|---|---|
| Internal policy assistant | RAG (928,575) | Decisive lead on freshness/grounding; not a near-tie. |
| High-volume structured extraction | Direct call (1,000,000) | Perfect fit; tool-using agent ranks last. |
| Human-supervised research | RAG ≈ Agent (821,425 vs 821,450) | Co-leading **near-tie** (25-unit gap, a documented Hamilton rounding artifact); workflow and direct clearly behind. |

## Changed files (high level)

- Solution/scaffold: `Vector.slnx`, `global.json`, `Directory.Build.props`, `.gitignore`,
  `portfolio.project.json`, seven projects under `src/` and `tests/`.
- Domain/Engine: `src/Vector.Domain/*.cs`, `src/Vector.Engine/*.cs`.
- Data/content: `src/Vector.Data/**`, `src/Vector.App/wwwroot/data/vector-knowledge.v1.json` (+ schema).
- UI: `src/Vector.App/**` (components, `StudioState`, styling, JS interop).
- Docs: `README.md`, `docs/decision-method.md`, `docs/limitations.md`, `docs/authoring-guide.md`,
  `docs/adr/000{1,2,3}-*.md`, this file.

## Risks and things to watch

- **Coarse model, honest by design.** Scores are ordinal (0–4 inputs). Small gaps are not meaningful;
  the near-tie flag and UI copy manage this, but reviewers should sanity-check the qualitative reads
  rather than the exact numbers.
- **Content calibration.** The capability matrix and demand curves were tuned to produce three
  intended qualitative reads; calibration tests lock them. Any content edit must re-run those tests
  (see `docs/authoring-guide.md`).
- **Scoring-model correction.** The engine uses a weighted **unmet-demand** model, not the
  `capability − demand` model first sketched — the latter was proven insensitive to level changes.
  See [ADR 0002](docs/adr/0002-transparent-mcda.md).
- **Sub-path hosting.** `index.html` uses `<base href="/">`; hosting under a sub-path (e.g. GitHub
  Pages project sites) requires changing it and providing SPA fallback.
- **Brotli in WebAssembly.** The browser WASM runtime (without the `wasm-tools` workload) does not
  provide native Brotli, so `ShareCodec` degrades to an uncompressed (raw) payload there — caught and
  handled in the codec, verified in-browser. Compression is a non-issue for the tiny share payload; the
  raw path stays far within the size cap. This surfaced only at runtime, not in the CoreCLR test suite.

## Cross-review questions (for the cross-model reviewer)

1. **Capability matrix bias.** Does the 4×8 capability matrix encode any unfair bias toward or against
   a pattern, independent of the scenarios? Is any single cell hard to defend on its own?
2. **Unmet-demand vs. alternatives.** Is the weighted unmet-demand model the right honesty/expressive
   trade-off, versus a shortfall-with-margin-reward or an explicit interaction model? Are the "close
   scores are ties" and near-tie treatments sufficient given no reward for over-meeting a demand?
3. **Hard-gate semantics.** Is deriving vetoes from `capability < demand` on a user-toggled hard flag
   clearer and safer than authored veto rules? Any scenario where it surprises?
4. **Share-URL bounds.** Are the 1800-char encoded cap and 4096-byte decompression bound appropriate?
   Any decode path that could still throw or over-allocate?
5. **Non-claims.** Does any UI/export string overstate what Vector concludes (e.g. implying a "best"
   or "compliant" architecture)?

## Constraints honored

No public visibility change, production promotion, or backdating in this lane. No private/certification
questions, employer code, secrets, or fabricated real-world outcomes entered the repository. All
scenario language is original.
