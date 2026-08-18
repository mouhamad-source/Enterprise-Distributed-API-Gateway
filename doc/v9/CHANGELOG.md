# Changelog

## v9.0.0 – Observability and Structured Telemetry

### Summary
This release introduces a comprehensive observability stack into the Gateway. By integrating structured logging, metrics, tracing, and correlation IDs, the system now supports enterprise‑grade monitoring, debugging, and performance analysis. The design aligns with open standards (OpenTelemetry, Prometheus, Jaeger) and ensures traceability across distributed services.

### Key Design Decisions
- **Structured Logging with Serilog (JSON)**  
  Logs enriched with `TraceId`, `RequestId`, `Method`, `Path`, `StatusCode`, and `Duration` for advanced search and analysis.
- **Metrics via OpenTelemetry + Prometheus Exporter**  
  Standards‑based metrics collection, scalable, and directly consumable by Prometheus/Grafana.
- **Tracing via OpenTelemetry + Jaeger Exporter**  
  Full support for W3C TraceContext, enabling request correlation across services and visualization of call chains.
- **Correlation ID Middleware + Activity**  
  Ensures `TraceId` is propagated across boundaries and included in logs and responses.
- **Separation of Responsibilities**  
  MetricsRegistry and ILogger injected via DI for testability and modularity.

### Implementation Highlights
- Added structured logging pipeline using Serilog with JSON output.
- Integrated OpenTelemetry metrics with Prometheus exporter, exposed via `/metrics`.
- Implemented OpenTelemetry tracing with Jaeger exporter for distributed trace visualization.
- Developed Correlation ID middleware to propagate identifiers across services.
- Refactored DI to inject `MetricsRegistry` and `ILogger` cleanly.

### Operational Impact
- Logs now provide rich contextual data for debugging and compliance.
- Metrics available at `/metrics` endpoint for Prometheus scraping.
- Traces exported to Jaeger (or OTLP) for end‑to‑end visualization of request flows.
- Health endpoint `/health` added for readiness checks.

### Challenges Faced
- **Telemetry Integration Complexity**  
  Aligning Serilog, OpenTelemetry, and Prometheus/Jaeger exporters required careful configuration to avoid conflicts.  
- **Correlation ID Propagation**  
  Ensuring consistent propagation across middleware and services demanded iterative testing and adjustments.  
- **Performance Considerations**  
  Balancing observability depth with minimal latency overhead was critical to maintain Gateway throughput.

