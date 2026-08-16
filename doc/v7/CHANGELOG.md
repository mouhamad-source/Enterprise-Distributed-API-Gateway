# Changelog

## v7.0.0 – Resilience Policies with Polly

### Summary
This release introduces a resilience layer to the Gateway using structured policies (Timeout, Retry, Circuit Breaker). The design isolates dependencies, enforces best‑practice ordering, and ensures robust handling of transient failures in distributed environments.

### Key Design Decisions
- **Abstract Interface (IServiceResiliencePolicy)**  
  Decoupled from Polly to allow future replacement or mocking in tests.
- **Policy Ordering (Timeout → Retry → Circuit Breaker)**  
  Timeout applies per attempt, retry re‑executes on transient failures, and circuit breaker protects the system from repeated failures. This follows industry standards.
- **Retry Only on Transient Errors**  
  Retries triggered for `5xx`, `408`, and network exceptions. No retries for `404` or `401` as they are non‑recoverable.
- **Global Circuit Breaker**  
  Applied across all services for simplicity in v7. Future improvement: per‑service or per‑route breakers.
- **EnableBuffering**  
  Ensures request body can be re‑read during retries.

### Implementation Highlights
- Added `IServiceResiliencePolicy` interface to abstract resilience logic.
- Integrated Timeout, Retry, and Circuit Breaker policies in correct sequence.
- Updated Gateway pipeline to apply resilience policies consistently.
- Enhanced error handling to distinguish transient vs permanent failures.
- Leveraged `EnableBuffering` for safe body re‑reads.

### Operational Impact
- Improved system stability under transient network issues.
- Reduced risk of cascading failures with circuit breaker protection.
- Clear separation of resilience logic for maintainability and testability.
