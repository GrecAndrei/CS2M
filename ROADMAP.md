# CS2M Gameplay-First Roadmap

Updated: 2026-02-06
Owner: Alex + Codex
Priority Rule: gameplay replication before UI features

## M1 - Network/State Foundation
- Status: `in progress` (code complete pending manual host/client smoke loop)
- Goal: stable connect/join/leave lifecycle with clear state boundaries.
- Scope:
  - complete join state machine transitions,
  - add strict command gating by state,
  - fix disconnect cleanup for client and server paths.
- Exit criteria:
  - repeated join/leave without restart,
  - no stuck `CONNECTION_ESTABLISHED`/`WAITING_TO_JOIN`,
  - no command processing from unauthorized peers.

## M2 - Authoritative Building Placement
- Status: `in progress` (protocol + hook + apply scaffold implemented, runtime validation pending)
- Goal: one authoritative gameplay action end-to-end.
- Scope:
  - client sends placement request command,
  - server validates and applies,
  - server replicates to all clients.
- Exit criteria:
  - same placed building appears on all peers in one session.

## M3 - Deterministic Core Sync Hardening
- Status: `in progress` (authority enforcement pass implemented, telemetry + long-session validation pending)
- Goal: reduce drift in long sessions.
- Scope:
  - enforce server authority for time/speed/frame/money,
  - add lightweight divergence detection and correction hooks.
- Exit criteria:
  - measurable drop in frame/simulation divergence during long play.

## M4 - Late Join Reliability
- Status: `in progress` (transfer integrity + retry hardening implemented, runtime validation pending)
- Goal: robust world transfer for players joining in progress.
- Scope:
  - harden save transfer and load error handling,
  - clean retry behavior.
- Exit criteria:
  - repeated late join works without manual recovery steps.

## M5 - Gameplay Surface Expansion
- Status: `in progress` (authoritative bulldoze + zoning + roads scaffolds implemented, runtime validation pending)
- Goal: add high-impact gameplay operations.
- Scope order:
  1. bulldoze
  2. zoning
  3. roads
- Exit criteria:
  - each feature uses same authoritative server pattern as M2.

## M6 - Test/Debug Infrastructure
- Goal: faster iteration with fewer regressions.
- Scope:
  - scriptable host/client smoke scenarios,
  - focused replication logs,
  - protocol-level tests where feasible.
- Exit criteria:
  - stable pre-merge verification checklist.
