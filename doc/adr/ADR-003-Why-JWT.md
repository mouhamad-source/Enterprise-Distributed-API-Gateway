# ADR-003: Why JWT for Authentication?

**Date:** 2026-08-19  
**Status:** Accepted

## Context
We need a stateless authentication mechanism that can be validated at the Gateway without database lookups.

## Decision
We use **JWT (JSON Web Tokens)** with HMAC-SHA256 for authentication.

## Alternatives Considered
- **Session-based (cookies)**: Requires sticky sessions or distributed session store.
- **OAuth2 with introspection**: Requires extra network calls to Auth Server.
- **API Keys**: Less secure and difficult to expire.

## Consequences
- **Pros**: Stateless, self-contained, easy to validate, supports claims (plan, role).
- **Cons**: Cannot revoke tokens easily (use short expiry + refresh tokens).
- **Trade-off**: Simplicity and performance over revocation control.

## Future Considerations
- Add OAuth2/OIDC with introspection for enterprise clients.
- Rotate signing keys regularly.