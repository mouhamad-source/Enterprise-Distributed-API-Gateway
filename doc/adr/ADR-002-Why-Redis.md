# ADR-002: Why Redis?

**Date:** 2026-08-19  
**Status:** Accepted

## Context
Rate limiting must be shared across multiple Gateway instances. We need a fast, atomic, and distributed counter store.

## Decision
We use **Redis** as the central store for rate‑limiting counters.

## Alternatives Considered
- **PostgreSQL**: Higher latency (10-50ms) and requires locks for atomic increments.
- **In-memory per instance**: Cannot share state across instances.
- **Memcached**: No built‑in atomic operations (INCR) with TTL in one command.

## Consequences
- **Pros**: Low latency (<1ms), atomic INCR/Lua scripts, TTL, high throughput (100k ops/sec).
- **Cons**: Additional infrastructure dependency (fail‑closed strategy to protect services).
- **Trade-off**: Accepting Redis dependency for performance.

## Future Considerations
- Redis Cluster for HA and higher throughput.
- Consider local caching with TTL to reduce Redis calls if needed.