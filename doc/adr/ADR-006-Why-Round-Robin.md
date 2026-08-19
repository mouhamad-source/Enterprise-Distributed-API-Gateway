# ADR-006: Why Round Robin Load Balancing?

**Date:** 2026-08-19  
**Status:** Accepted

## Context
We need to distribute requests across multiple backend instances.

## Decision
We use **Round Robin** as the default load‑balancing algorithm.

## Alternatives Considered
- **Random**: Simple but can be uneven.
- **Least Connections**: Requires connection tracking per instance.
- **Weighted**: Requires manual weights; not needed initially.

## Consequences
- **Pros**: Simple, fair, easy to implement, no state required.
- **Cons**: Does not consider instance load or response time.
- **Trade-off**: Simplicity over adaptive distribution.

## Future Considerations
- Add Least Connections or response‑time‑based algorithm.
- Allow per‑service algorithm selection.