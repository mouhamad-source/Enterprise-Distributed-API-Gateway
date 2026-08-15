# Identity Resolution Layer – Design Document

## Executive Summary
The Identity Resolution Layer (v4.0.0) introduces user identity awareness into the Gateway pipeline. This evolution moves beyond IP-based rate limiting, enabling enforcement per authenticated entity. Strategically, this ensures fairness, compliance, and resilience in distributed environments.

## Problem Statement
- IP-based limits are insufficient:
  - Shared IPs cause unfair throttling.
  - Malicious actors can rotate IPs to bypass limits.
- The system requires **identity-based enforcement** aligned with modern API security standards.

## Design Goals
1. **Accuracy** – Resolve client identity via tokens, API keys, or sessions.
2. **Isolation** – Independent counters per identity.
3. **Transparency** – User services remain unaware of rate limiting logic.
4. **Performance** – Sub-millisecond overhead per request.
5. **Resilience** – Fail gracefully if identity resolution fails.

## Architecture
The Identity Layer sits between Gateway ingress and Rate Limiter. It extracts and normalizes identity before passing it to Redis for distributed counting.

## Flow
1. Request arrives at Gateway.
2. Identity Resolution Layer extracts headers/tokens.
3. Identity normalized (e.g., `UserID:12345`).
4. Rate Limiter applies distributed counter via Redis.
5. Decision: Allow or reject request.
6. Forward approved requests to User Service.

## Strategic Scenarios
- **Banking API** – Fail Closed for maximum security.
- **Public Content API** – Fail Open for availability.
- **Multi-Tenant SaaS** – Tenant isolation ensures fairness across customers.

## Risks & Mitigations
- **Token Spoofing** – Use cryptographic validation (JWT, HMAC).
- **Redis Outage** – Define fail-open vs fail-closed per service.
- **Latency Impact** – Optimize with local caching of identity mappings.

## Next Steps
- v5.0.0 – Advanced algorithms (sliding window, token bucket).
- Extend documentation with `OPERATIONS.md` and `TEST_PLAN.md`.
