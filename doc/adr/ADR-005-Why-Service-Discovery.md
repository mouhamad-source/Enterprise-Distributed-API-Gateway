# ADR-005: Why Service Discovery?

**Date:** 2026-08-19  
**Status:** Accepted

## Context
Backend instances are dynamic (scaling, failures, deployments). Hard‑coding addresses is not feasible.

## Decision
We implement a **Service Registry** (in‑memory with health checks) that tracks healthy instances.

## Alternatives Considered
- **Static configuration**: Requires redeployment on every change.
- **DNS-based discovery**: Slow TTL propagation.
- **Consul/etcd**: More complex, adds operational overhead.

## Consequences
- **Pros**: Dynamic, resilient to instance changes, health‑aware.
- **Cons**: In‑memory registry is not persistent (but acceptable for our scale).
- **Trade-off**: Simplicity vs. scalability.

## Future Considerations
- Migrate to Consul or Kubernetes native service discovery.
- Add caching to reduce registry lookups.