# Architecture Overview

## Vision
The Gateway project is designed to provide a scalable, resilient, and secure entry point for client requests. Each version represents a deliberate architectural evolution to address new challenges in distributed systems.

## Evolution
- **v1.0.0 – Basic Gateway**  
  Problem: Routing requests to the correct service.  
  Solution: Gateway abstraction.

- **v2.0.0 – In-Memory Rate Limiter**  
  Problem: Preventing resource exhaustion by a single client.  
  Solution: Local memory counters.

- **v3.0.0 – Distributed Rate Limiter (Redis)**  
  Problem: Synchronizing limits across multiple gateways.  
  Solution: Shared state via Redis with atomic operations and TTL.

- **v4.0.0 – Identity Resolution Layer**  
  Problem: Distinguishing users beyond IP (e.g., tokens, identities).  
  Solution: Identity resolution middleware integrated into the gateway pipeline.

## Key Principles
1. **Scalability** – Horizontal gateway scaling with shared state.  
2. **Resilience** – Fail‑open vs fail‑closed strategies for Redis outages.  
3. **Security** – Identity resolution ensures fair usage per authenticated entity.  
4. **Performance** – Atomic operations with minimal latency overhead.  
5. **Maintainability** – Clear separation between business data and infrastructure data.

## Next Steps
- v5.0.0 – Advanced Rate Limiting Algorithms (e.g., sliding window, token bucket).  
- Continuous documentation in `/doc` for each layer (Design, Flow, Testing, Operations).
