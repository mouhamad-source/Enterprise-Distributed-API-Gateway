# Changelog

## v5.0.0 – Advanced Rate Limiting Algorithms

### Summary
This release introduces a modular, extensible rate limiting framework that supports multiple algorithms and ensures atomicity in distributed environments. It represents a strategic evolution from infrastructure‑level counters to algorithmic flexibility.

### Key Design Decisions
- **Strategy Pattern** – Decoupled rate limiting algorithms from storage and routing, enabling easy extension.
- **Redis Lua Scripts** – Ensured atomic operations for Sliding Window and Token Bucket under high concurrency.
- **Configuration via appsettings.json** – Allowed algorithm selection without code changes, supporting DevOps agility.
- **RateLimiterService Context** – Centralized orchestration with dependency injection for Redis connections.
- **Enhanced Response Metadata** – Exposed `currentCount` for integration with headers (`X-RateLimit-Remaining`, `Retry-After`).

### Implementation Highlights
- Added `Gateway/RateLimiting/Strategies/` with:
  - `IRateLimitingStrategy`
  - `FixedWindowStrategy`
  - `SlidingWindowStrategy`
  - `TokenBucketStrategy`
  - `LeakyBucketStrategy`
- Introduced `RateLimiterService.cs` to manage strategy execution.
- Updated `RateLimitingConfig.cs` with `Algorithm` property.
- Modified `Program.cs` to register `RateLimiterService`.
- Removed legacy `RedisRateLimiter.cs`.
- Updated `appsettings.json` with `"Algorithm": "SlidingWindow"`.

### Operational Impact
- Supports multiple algorithms with minimal latency overhead.
- Ensures atomicity and resilience in distributed gateway clusters.
- Provides flexibility for future extensions (e.g., ML‑driven adaptive algorithms).

### Testing & Validation
- Verified algorithm switching via configuration changes.
- Conducted concurrency tests with Python scripts to validate atomic increments.
- Confirmed Redis TTL behavior for automatic counter expiration.

---
