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
- **Separation of Responsibilities**  
  Registry, Load Balancer, and Reverse Proxy are independent modules with clear contracts.
- **Dynamic Discovery**  
  Instances can be added or removed without restarting the Gateway.
- **Load Balancing**  
  Requests distributed across instances using Round Robin, preventing overload of a single instance.
- **Flexibility**  
  Load balancing algorithms can be swapped without modifying core gateway logic.
- **Scalability**  
  InMemoryServiceRegistry can be replaced with external solutions (e.g., Consul, Kubernetes DNS) for production environments.

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

### Next Steps
- Explore adaptive load balancing algorithms (latency‑aware, least connections).
- Integrate external service registries (Consul, Eureka, Kubernetes DNS).
- Extend reverse proxy features with path rewriting and custom header injection.
