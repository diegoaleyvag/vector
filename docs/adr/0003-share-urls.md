# ADR 0003 — Versioned, bounded share URLs

- **Status:** Accepted
- **Date:** 2026-08-13

## Context

A user should be able to share a configuration by URL so a colleague can reproduce the same analysis.
Vector has no backend, so the link must be self-contained. It must carry **no secrets**, round-trip a
valid profile, and **reject unsupported or oversized payloads** — a hostile or malformed link must
never crash the app or blow up memory.

## Decision

`Vector.Data.Sharing.ShareCodec` — a **pure, total (never-throwing)** codec:

- **Transport in the URL fragment (`#`), not the query string.** The fragment is never sent to any
  server, CDN, or analytics proxy, so the payload cannot be logged even in a fronted deployment, and
  changing it triggers no network round-trip. It is a static SPA, so the usual downsides of fragments
  do not apply.
- **Minimal payload, no free text.** Only `{ scenarioId?, levels[8], weightTiers[8], hard[8],
  rulesVersion }` in canonical dimension order — small integers, not doubles. **Rationale text is never
  included**, which keeps the "no secrets" guarantee easy and the link short.
- **Pipeline:** compact source-generated JSON → Brotli (keep whichever of raw/compressed is smaller,
  flagged by a 1-byte header) → Base64Url (no padding) → `v1.` version prefix *outside* the blob, so
  the version is checked before any bytes are trusted.
- **Validate-then-trust decode**, in order: empty → bad/absent version → **encoded-length cap (1800
  chars) checked before any decoding** → strict Base64Url → **Brotli decompression bounded to 4096
  bytes** (a fixed output buffer defeats decompression-bomb inputs) → deserialize → semantic
  validation (exactly 8 entries per array; levels 0–4; tiers 0–3; bounded id/version lengths). Any
  failure returns a typed `ShareError`; the codec never throws.
- **Rejection UX:** a bad link loads a blank profile and shows a dismissible `role="status"` banner
  with a plain-language reason ("this link is for a different version of Vector" / "too large" /
  "malformed"). It never partially applies.

The digest that proves reproducibility is computed by the **engine** from the same canonical profile
the link encodes, so pasting a link and regenerating the record reproduces the same digest.

## Consequences

- **Positive:** shareable, backend-free, privacy-preserving, and robust against malformed/oversized/
  malicious input (fuzzed to confirm `Decode` never throws); a `v2` payload is rejected cleanly rather
  than mis-parsed.
- **Trade-offs:** the rationale does not travel in the link (it lives only in the exported Markdown) —
  a deliberate privacy choice; the 1800-char cap bounds how much state a link can carry, which is
  ample for 8 constraints but would need revisiting if the model grew large.
