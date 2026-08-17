# Changelog

## v8.0.0 – Modular Gateway with Service Registry, Load Balancer, and Reverse Proxy

### Summary

This release restructures the Gateway into a modular architecture by separating the Service Registry, Load Balancer, and Reverse Proxy. Each component now has a single responsibility, improving testability, scalability, and operational flexibility. The update also introduces dynamic instance discovery, automated health checks, and a standardized Round Robin load balancing strategy.

### Key Design Decisions

- **Service Registry Separation**  
  Each component (Registry, Load Balancer, Reverse Proxy) now has its own interface and implementation, simplifying testing and replacement.
- **Round Robin as Default Algorithm**  
  Provides fairness and simplicity for most workloads. Future algorithms (e.g., Least Connections) can be added without core code changes.
- **Automated Health Checks**  
  Instances are periodically validated and unhealthy ones removed automatically, ensuring resilient traffic distribution.
- **Reverse Proxy Integration**  
  Unified request forwarding logic inside the Gateway, enabling advanced features such as path rewriting and header injection.

### New Features

- **Separation of Responsibilities** – Independent modules with clear contracts.
- **Dynamic Discovery** – Instances can be added or removed without restarting the Gateway.
- **Load Balancing** – Round Robin prevents overload of a single instance.
- **Flexibility** – Algorithms can be swapped without modifying core gateway logic.
- **Scalability** – Registry can be replaced with Consul or Kubernetes DNS in production.

### Challenges Faced

- **Interface Consistency**  
  Significant effort was required to align method signatures (e.g., `GetInstance` vs `GetInstances`). This consumed ~2 hours of debugging due to strict interface implementation requirements.
- **Registry Configuration**  
  Integrating `UserService` instances into the registry required ~1 hour of troubleshooting. The issue stemmed from JSON incompatibility with `ServiceRegistry` configuration and adjustments in `Program.cs`.
- **Engineering Reality**  
  These challenges highlight the complexity of modular design. Developers should anticipate extended debugging sessions and configuration alignment when separating responsibilities at this scale.

### Implementation Highlights

- Refactored Gateway to isolate registry, load balancer, and reverse proxy.
- Configured Round Robin as the default balancing strategy.
- Added periodic health checks for dynamic instance management.
- Enhanced reverse proxy logic for consistent request routing and extensibility.

### Operational Impact

- Improved modularity and maintainability.
- Increased resilience with automatic removal of unhealthy instances.
- Simplified extension paths for future load balancing strategies.
- Enabled advanced routing features directly within the Gateway.
