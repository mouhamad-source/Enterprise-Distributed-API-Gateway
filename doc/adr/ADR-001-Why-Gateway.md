# ADR-001: Why an API Gateway?

**Date:** 2026-08-19  
**Status:** Accepted

## Context
We need to route requests to multiple microservices and enforce rate limiting, authentication, and resilience. Embedding these concerns in each service would lead to duplication and tight coupling.

## Decision
We implement a dedicated **API Gateway** that centralizes cross-cutting concerns: routing, rate limiting, authentication, resilience, and observability.

## Alternatives Considered
- **Embed in each service**: Causes code duplication and inconsistent behavior.
- **Third-party gateway (NGINX, Kong)**: Less flexibility for custom logic (JWT validation, dynamic rate limiting).
- **Service Mesh (Istio)**: Overkill for our current scale; adds operational complexity.

## Consequences
- **Pros**: Centralized control, easier to update policies, consistent logging/metrics.
- **Cons**: Single point of failure (mitigated by multiple instances + load balancer).
- **Trade-off**: Slightly increased latency due to extra hop (acceptable <5ms).

## Future Considerations
- Consider service mesh if microservices count exceeds 50.
- Add caching layer to reduce repeated calls to backend services.