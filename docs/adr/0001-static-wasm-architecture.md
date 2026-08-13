# ADR 0001 — Static Blazor WebAssembly, no backend

- **Status:** Accepted
- **Date:** 2026-08-13

## Context

Vector is a transparent decision-support studio. It must be trivially hostable and privacy-preserving,
carry no secrets, and keep the decision logic inspectable. It has no need to persist user data or call
any external model.

## Decision

Build Vector as a **static Blazor WebAssembly** application (.NET 10, C#) with **no backend, no
database, no server-side code, no API key, no authentication, and no persistence**. Versioned
scenario/rule data ships as **static JSON assets** under `wwwroot/data/` and is loaded over
`HttpClient` at startup.

The solution is split so the decision logic never depends on the UI or on serialization:

```
Vector.Domain   (pure records/enums; no Blazor, no JSON)
Vector.Engine   (pure MCDA algorithm + digest; depends only on Domain)
Vector.Data     (System.Text.Json DTOs + mappers + share codec + Markdown export; depends on Domain/Engine)
Vector.App      (Blazor WASM; the only project that references Blazor)
```

A NetArchTest-style reflection invariant fails the build if `Vector.Domain` or `Vector.Engine` ever
references Blazor or a JSON library.

## Consequences

- **Positive:** hostable on any static host (GitHub Pages, object storage, a CDN); nothing to operate;
  no data leaves the browser; the engine is unit-testable with zero UI/serialization surface; trimming
  keeps the download small.
- **Negative / trade-offs:** all computation runs client-side (fine — the model is tiny: 4 patterns ×
  8 constraints); first load pays the WASM runtime download; sub-path hosting requires setting
  `<base href>`; there is no server to validate share links, so validation must be client-side and
  defensive (see [ADR 0003](0003-share-urls.md)).
- **Deferred:** AOT compilation is *off* for v1 — it roughly doubles the download for no benefit on a
  trivial hot path. Trimming is on; `InvariantGlobalization` is on (English-only UI, and it guarantees
  culture-invariant numbers). Source-generated `System.Text.Json` is used so trimming stays safe.
