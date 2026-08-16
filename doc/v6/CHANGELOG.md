# Changelog

## v6.0.0 – Authentication Layer with JWT

### Summary

This release introduces a full authentication layer to the Gateway, enabling identity‑aware rate limiting and secure request validation. By integrating JWT, the system now enforces limits per user plan (Free, Premium, Admin) and ensures compliance with modern API security standards.

### Key Additions

- **Authentication Folder** with four new files:
  - `JwtSettings` – Configuration for JWT.
  - `JwtTokenValidator` – Validates JWT and extracts claims.
  - `UserContext` – Carries user identity across the pipeline.
  - `AuthenticationMiddleware` – Enforces authentication at gateway ingress.

### Modifications

- **IRateLimiter & RateLimiterService** – Extended to support `UserContext` and apply plan‑specific limits.
- **RateLimitingConfig** – Added `DefaultLimit` property.
- **CompositeResolver** – Removed `JwtResolver` (no longer required).
- **GatewayMiddleware** – Updated to consume `UserContext` from `HttpContext.Items`.
- **Program.cs** – Registered `AuthenticationMiddleware` at the start of the pipeline.
- **appsettings.json** – Added `JwtSettings` section.

### Removals

- Deleted `Identification/JwtResolver.cs` (deprecated).

### Operational Impact

- Requests are now authenticated via JWT before rate limiting.
- Limits can be differentiated by user plan, improving fairness and monetization.
- Security posture strengthened with token validation and claim extraction.

### testing base on test_6.py we get this successful test :

--- Authentication Tests ---
Valid token: 200 (expected 200)
Expired token: 401 (expected 401)
Invalid signature: 401 (expected 401)
Wrong issuer: 401 (expected 401)
Wrong audience: 401 (expected 401)

--- Rate Limit Tests ---
Free: 100 succeeded (expected <= 100)
Premium: 200 succeeded (expected <= 5000)
Admin: 200 succeeded (expected <= 200)
No token: 429 (expected 200, 401, or 429)

small notes in the test we set like JWT token this is not correct but this is just for test in the realy application we need to creating a .env file and set all secrte key in it with out push on GitHub
