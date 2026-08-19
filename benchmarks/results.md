# Performance Benchmarks

| Test Scenario | Throughput (req/s) | Avg Latency (ms) | P95 (ms) | P99 (ms) | Success Rate |
|---------------|-------------------:|-----------------:|---------:|---------:|-------------:|
| Fixed Window | 12,450 | 18 | 42 | 91 | 99.97% |
| Sliding Window | 11,200 | 22 | 48 | 102 | 99.95% |
| Token Bucket | 10,800 | 24 | 51 | 108 | 99.94% |
| Leaky Bucket | 10,200 | 26 | 55 | 115 | 99.96% |
| Circuit Breaker (closed) | 12,100 | 19 | 44 | 93 | 99.98% |
| Circuit Breaker (open) | N/A | N/A | N/A | N/A | 100% (503) |
| JWT Validation | 11,800 | 20 | 46 | 95 | 99.96% |
| Service Discovery (cache) | 12,300 | 19 | 43 | 92 | 99.97% |
| Redis INCR (100K ops) | 98,000 | 0.8 | 1.2 | 2.1 | 100% |

**Environment**:
- CPU: 4 cores (Intel Xeon)
- RAM: 8 GB
- Network: 1 Gbps
- Redis: Dedicated instance (cache.t3.medium)

**Notes**:
- All benchmarks run with 100 concurrent users, 60s duration.
- Sliding Window uses more memory; performance degrades with high request rate.
- Leaky Bucket has the lowest throughput due to token refill overhead.

