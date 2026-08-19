# ADR-004: Why Circuit Breaker?

**Date:** 2026-08-19  
**Status:** Accepted

## Context
When a backend service fails, continuing to forward requests wastes resources and causes cascading failures.

## Decision
We implement a **Circuit Breaker pattern** (via Polly) to stop forwarding requests to unhealthy services.

## Alternatives Considered
- **Retry only**: Can overwhelm a failing service.
- **Timeout only**: Still sends requests, causing thread pool exhaustion.
- **Manual intervention**: Not acceptable for production.

## Consequences
- **Pros**: Prevents cascading failures, gives service time to recover.
- **Cons**: Adds complexity, requires tuning (5 failures, 30s break).
- **Trade-off**: Availability vs. reliability.

## Future Considerations
- Make circuit breaker thresholds configurable per service.
- Integrate with service health checks for automatic recovery.