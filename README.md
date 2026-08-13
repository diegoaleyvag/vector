# Vector

**Which architecture fits the constraints?**

Vector is a transparent **decision-support studio** for comparing AI-system architecture patterns
under explicit constraints, and for exporting a traceable, ADR-style decision record. It is *not* an
"AI architect" that declares a universal answer — inputs, weights, rules, trade-offs, and uncertainty
all stay visible and inspectable.

It compares four bounded patterns — **Direct structured model call**, **Deterministic workflow with
model steps**, **Retrieval-augmented generation (RAG)**, and **Tool-using agent** — across eight
constraints (data sensitivity, latency, cost pressure, determinism, knowledge freshness, tool/action
need, human review, operational maturity), separating **hard requirements** from **weighted
preferences**.

> Vector is a decision-support teaching tool. It does not tell you the "correct" or "best"
> architecture, it is not compliance or professional advice, and its three scenarios are original
> educational examples, not real deployments. See [docs/limitations.md](docs/limitations.md).

## What you can do

1. Start from a blank profile or one of three original scenarios.
2. Adjust eight constraints, their importance, and whether each is a hard requirement.
3. Compare the four patterns, with every score contribution and hard conflict inspectable.
4. See risks and mitigations tied to the leading patterns.
5. Test sensitivity — which single change would flip the leading option.
6. Write your own rationale (Vector never writes it for you).
7. Download an ADR-style Markdown decision record.
8. Share the configuration through a versioned, bounded, validated URL.

## Tech stack

.NET 10 · C# · Blazor WebAssembly (static, no backend/database/model/API key) · xUnit · bUnit ·
versioned JSON rule data. The decision engine is pure and independent of the UI and of serialization.

## Quick start

Requires the **.NET 10 SDK** (`10.0.x`). On macOS with Homebrew:

```bash
brew install dotnet
```

Then, from the repository root:

```bash
dotnet build Vector.slnx
```

```bash
dotnet test Vector.slnx
```

```bash
dotnet run --project src/Vector.App
```

The last command serves the app locally; open the printed URL in a browser.

## Project structure

| Project | Role |
|---|---|
| `src/Vector.Domain` | Pure records/enums: scenarios, constraints, patterns, results. No Blazor, no JSON. |
| `src/Vector.Engine` | The deterministic MCDA algorithm and configuration digest. Depends only on Domain. |
| `src/Vector.Data` | JSON DTOs + mappers, the share codec, and the Markdown exporter. Owns all serialization. |
| `src/Vector.App` | The Blazor WebAssembly UI. The only project that references Blazor. |
| `tests/Vector.Engine.Tests` | Engine invariants (determinism, veto dominance, sensitivity, digest, architecture). |
| `tests/Vector.Data.Tests` | Content load/mapping, calibration against the real engine, share-codec fuzzing, Markdown export. |
| `tests/Vector.App.Tests` | bUnit tests of the core UI flows. |

Rule/scenario content is a single versioned file:
[`src/Vector.App/wwwroot/data/vector-knowledge.v1.json`](src/Vector.App/wwwroot/data/vector-knowledge.v1.json).

## Documentation

- [The decision method](docs/decision-method.md) — scales, demand curves, the unmet-demand scoring
  model, weights, hard gating, near-tie, sensitivity, and the digest.
- [Limitations and non-claims](docs/limitations.md).
- [Authoring guide](docs/authoring-guide.md) — adding scenarios/patterns without breaking determinism.
- Architecture decision records: [static WASM](docs/adr/0001-static-wasm-architecture.md) ·
  [transparent MCDA](docs/adr/0002-transparent-mcda.md) · [share URLs](docs/adr/0003-share-urls.md).
- [HANDOFF.md](HANDOFF.md) — commands, test evidence, and open questions.
